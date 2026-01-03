using System.Collections.Generic;
using Npgsql;

namespace AgeDigitalTwins;

public static class GraphInitialization
{
    public static List<NpgsqlBatchCommand> GetGraphInitCommands(string graphName)
    {
        return
        [
            new(@$"SELECT create_vlabel('{graphName}', 'Twin');"),
            new(
                @$"CREATE UNIQUE INDEX twin_id_idx ON {graphName}.""Twin"" (ag_catalog.agtype_access_operator(properties, '""$dtId""'::agtype));"
            ),
            new(
                @$"CREATE INDEX twin_model_id_idx ON {graphName}.""Twin"" (ag_catalog.agtype_access_operator(properties,'""$metadata""'::agtype,'""$model""'::agtype));"
            ),
            new(@$"CREATE INDEX twin_gin_idx ON {graphName}.""Twin"" USING gin (properties);"),
            new(@$"ALTER TABLE {graphName}.""Twin"" REPLICA IDENTITY FULL;"),
            new(@$"SELECT create_vlabel('{graphName}', 'Model');"),
            new(
                @$"CREATE UNIQUE INDEX model_id_idx ON {graphName}.""Model"" (ag_catalog.agtype_access_operator(properties, '""id""'::agtype));"
            ),
            new(@$"CREATE INDEX model_gin_idx ON {graphName}.""Model"" USING gin (properties);"),
            new(
                @$"CREATE INDEX IF NOT EXISTS model_bases_gin_idx ON {graphName}.""Model"" 
                USING gin ((ag_catalog.agtype_access_operator(properties,'""bases""'::agtype)));"
            ),
            new(@$"ALTER TABLE {graphName}.""Model"" REPLICA IDENTITY FULL;"),
            new(@$"SELECT create_elabel('{graphName}', '_extends');"),
            new(@$"ALTER TABLE {graphName}.""_extends"" REPLICA IDENTITY FULL;"),
            // Add indexes on _extends table for optimized inheritance queries
            new(
                @$"CREATE INDEX IF NOT EXISTS _extends_start_id_idx ON {graphName}.""_extends"" (start_id);"
            ),
            new(
                @$"CREATE INDEX IF NOT EXISTS _extends_end_id_idx ON {graphName}.""_extends"" (end_id);"
            ),
            new(@$"SELECT create_elabel('{graphName}', '_hasComponent');"),
            new(@$"ALTER TABLE {graphName}.""_hasComponent"" REPLICA IDENTITY FULL;"),
        ];
    }

    public static List<NpgsqlBatchCommand> GetGraphUpdateFunctionsCommands(string graphName)
    {
        return
        [
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_of_model(twin agtype, model_id agtype, exact boolean default false)
                RETURNS boolean
                LANGUAGE plpgsql
                STABLE
                AS $function$
                DECLARE
                    twin_props agtype;
                    twin_model_id agtype;
                    model_descendants agtype;
                BEGIN
                    -- Extract properties whether twin is a vertex or already a map
                    BEGIN
                        twin_props := ag_catalog.age_vertex_get_properties(twin);
                    EXCEPTION WHEN others THEN
                        twin_props := twin;
                    END;

                    twin_model_id := ag_catalog.agtype_access_operator(twin_props,'""$metadata""','""$model""');
                    IF twin_model_id IS NULL THEN
                        RETURN false; -- Missing model metadata
                    END IF;

                    -- Fast path: direct match
                    IF twin_model_id = model_id THEN
                        RETURN true;
                    END IF;

                    -- If model_id is an array, check if twin_model_id is in it
                    BEGIN
                        IF model_id @> ag_catalog.agtype_build_list(twin_model_id) THEN
                            RETURN true;
                        END IF;
                    EXCEPTION WHEN others THEN
                        -- model_id is not an array, ignore
                    END;

                    -- Exact requested, no inheritance traversal
                    IF exact THEN
                        RETURN false;
                    END IF;

                    -- Try fast path: use precomputed descendants from model if available
                    BEGIN
                        SELECT ag_catalog.agtype_access_operator(m.properties,'""descendants""'::agtype)
                        INTO model_descendants
                        FROM {graphName}.""Model"" m
                        WHERE ag_catalog.agtype_access_operator(m.properties,'""id""'::agtype) = model_id;
                        
                        IF model_descendants IS NOT NULL THEN
                            -- Model has precomputed descendants array, use agtype containment check
                            -- Check if twin_model_id is in the descendants array using agtype operators
                            RETURN model_descendants @> ag_catalog.agtype_build_list(twin_model_id);
                        END IF;
                    EXCEPTION WHEN others THEN
                        -- Descendants field missing or error, fall through to legacy traversal
                    END;

                    -- Fallback: legacy inheritance traversal for backward compatibility
                    -- (models without descendants field)
                    -- Check inheritance via bases array
                    EXECUTE format('SELECT m FROM ag_catalog.cypher(''{graphName}'', $$
                        MATCH (m:Model)
                        WHERE %s IN m.bases
                        RETURN collect(m.id)
                    $$) AS (m agtype)', model_id)
                    INTO models_array;
                    
                    -- Check if twin's model ID is in the collected models array
                    RETURN models_array @> ag_catalog.agtype_build_list(twin_model_id);
                END;
                $function$"
            ),
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.agtype_set(target agtype, path agtype, new_value agtype)
                RETURNS agtype 
                AS $$
                DECLARE
                    json_target jsonb;
                    json_new_value jsonb;
                    text_path text[];
                BEGIN
                    -- Cast agtype to jsonb
                    json_target := target::json::jsonb;

                    -- Convert agtype path to text array
                    text_path := ARRAY(SELECT json_array_elements_text(path::json));

                    BEGIN
                        -- Attempt to cast new_value to jsonb directly
                        json_new_value := new_value::json::jsonb;
                    EXCEPTION
                        WHEN others THEN
                            -- Handle different types of new_value
                            IF new_value::text ~ '^[0-9]+$|^[0-9]*\.[0-9]+$|^[0-9]+(\.[0-9]+)?[eE][+-]?[0-9]+$' THEN
                                -- Convert all numeric values to float
                                json_new_value := to_jsonb(new_value::float);
                            ELSIF new_value::text = 'true' OR new_value::text = 'false' THEN
                                -- Boolean
                                json_new_value := to_jsonb(new_value::boolean);
                            ELSE
                                -- Default to text
                                json_new_value := to_jsonb(new_value);
                            END IF;
                    END;

                    -- Use jsonb_set to update the json value
                    json_target := jsonb_set(json_target::jsonb, text_path, json_new_value);

                    -- Cast the result back to agtype
                    RETURN json_target::text::agtype;
                END;
                $$ LANGUAGE plpgsql;"
            ),
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.agtype_delete_key(target agtype, path agtype)
                RETURNS agtype
                AS $$
                DECLARE
                    json_target jsonb;
                    text_path text[];
                BEGIN
                    -- Cast agtype to jsonb
                    json_target := target::json::jsonb;

                    -- Convert agtype path to text array
                    text_path := ARRAY(SELECT json_array_elements_text(path::json));

                    -- Use jsonb delete key to update the json value
                    json_target := json_target  #- text_path;

                    -- Cast the result back to agtype
                    RETURN json_target::text::agtype;
                END;
                $$ LANGUAGE plpgsql;"
            ),
            // IS_OBJECT: returns true if agtype is a map/object
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_object(val agtype)
                RETURNS boolean 
                AS $$
                BEGIN
                    RETURN ag_catalog.age_keys(val) IS NOT NULL;
                EXCEPTION
                    WHEN others THEN
                        RETURN false;
                END;
                $$ LANGUAGE plpgsql;"
            ),
            // IS_NUMBER: returns true if agtype is number
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_number(val agtype)
                RETURNS boolean
                AS $$
                BEGIN
                    RETURN (ag_catalog.age_tofloat(val) IS NOT NULL OR ag_catalog.age_tointeger(val) IS NOT NULL) AND NOT (ag_catalog.age_tostring(val) = val);
                EXCEPTION
                    WHEN others THEN
                        RETURN false;
                END;
                $$ LANGUAGE plpgsql;"
            ),
            // IS_PRIMITIVE: returns true if agtype is string, number, boolean
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_primitive(val agtype)
                RETURNS boolean
                AS $$
                BEGIN
                    RETURN ag_catalog.age_tostring(val) IS NOT NULL OR ag_catalog.age_tofloat(val) IS NOT NULL OR val = true OR val = false;
                EXCEPTION
                    WHEN others THEN
                        RETURN false;
                END;
                $$ LANGUAGE plpgsql;"
            ),
            // IS_STRING: returns true if agtype is a string
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_string(val agtype)
                RETURNS boolean
                AS $$
                BEGIN
                    RETURN ag_catalog.age_tostring(val) = val;
                EXCEPTION
                    WHEN others THEN
                        RETURN false;
                END;
                $$ LANGUAGE plpgsql;"
            ),
            new(
                // NOTE: This function returns the model id itself plus its descendants.
                @$"CREATE OR REPLACE FUNCTION {graphName}.model_and_descendants(model_id agtype)
                RETURNS agtype
                LANGUAGE plpgsql
                STABLE
                AS $function$
                DECLARE
                    descendants agtype;
                BEGIN
                    SELECT ag_catalog.agtype_access_operator(m.properties, '""descendants""'::agtype)
                    INTO descendants
                    FROM {graphName}.""Model"" m
                    WHERE ag_catalog.agtype_access_operator(m.properties, '""id""'::agtype) = model_id;
                    IF descendants IS NULL THEN
                        RETURN ag_catalog.agtype_build_list(model_id); -- Only itself if not found
                    END IF;
                    RETURN ag_catalog.agtype_build_list(model_id) || descendants; -- Itself + descendants
                END;
                $function$"
            ),
        ];
    }
}
