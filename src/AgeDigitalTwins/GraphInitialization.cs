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
                            -- Model has precomputed descendants, check membership
                            RETURN twin_model_id = ANY(
                                SELECT jsonb_array_elements_text(model_descendants::jsonb)::text::agtype
                            );
                        END IF;
                    EXCEPTION WHEN others THEN
                        -- Descendants field missing or error, fall through to legacy traversal
                    END;

                    -- Fallback: legacy inheritance traversal for backward compatibility
                    -- (models created before descendants field was added)
                    RETURN EXISTS (
                        WITH RECURSIVE ancestors AS (
                            SELECT m.id AS internal_id,
                                   ag_catalog.agtype_access_operator(m.properties,'""id""'::agtype) AS mid
                            FROM {graphName}.""Model"" m
                            WHERE ag_catalog.agtype_access_operator(m.properties,'""id""'::agtype) = twin_model_id
                            UNION ALL
                            SELECT parent.id,
                                   ag_catalog.agtype_access_operator(parent.properties,'""id""'::agtype) AS mid
                            FROM ancestors a
                            JOIN {graphName}.""_extends"" e ON e.start_id = a.internal_id
                            JOIN {graphName}.""Model"" parent ON parent.id = e.end_id
                        )
                        SELECT 1 FROM ancestors WHERE mid = model_id
                    );
                END;
                $function$"
            ),
            // Helper: return array of model ids that are descendants (child, grandchild, ...) of the given model_id.
            // include_self=true will also include the input model_id.
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.get_model_descendants_ids(model_id agtype, include_self boolean default true)
                RETURNS agtype[]
                STABLE
                AS $$
                WITH RECURSIVE descendants AS (
                    -- Optionally include the model_id itself
                    SELECT m.id AS internal_id,
                           ag_catalog.agtype_access_operator(m.properties, '""id""'::agtype) AS model_name
                    FROM {graphName}.""Model"" m
                    WHERE include_self AND ag_catalog.agtype_access_operator(m.properties, '""id""'::agtype) = model_id

                    UNION ALL

                    -- Traverse children via _extends: child (start_id) -> parent (end_id)
                    SELECT child.id AS internal_id,
                           ag_catalog.agtype_access_operator(child.properties, '""id""'::agtype) AS model_name
                    FROM descendants d
                    JOIN {graphName}.""_extends"" e ON e.end_id = d.internal_id
                    JOIN {graphName}.""Model"" child ON child.id = e.start_id
                )
                SELECT ARRAY_AGG(model_name) FROM descendants;
                $$ LANGUAGE sql;"
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
            /*
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_of_model_old2(twin agtype, model_id agtype, exact boolean default false)
                RETURNS boolean
                LANGUAGE plpgsql
                STABLE
                AS $function$
                DECLARE
                    twin_model_id text;
                    model_id_text text;
                BEGIN
                    -- Extract model ID from twin metadata
                    twin_model_id := ag_catalog.agtype_access_operator(twin,'""$metadata""'::agtype,'""$model""'::agtype)::text;
                    -- Remove quotes from agtype string values
                    twin_model_id := trim(both '""' from twin_model_id);
                    model_id_text := trim(both '""' from model_id::text);
                    
                    -- Direct match check first (most common case)
                    IF twin_model_id = model_id_text THEN
                        RETURN true;
                    END IF;
                    
                    -- If exact match required, return false if direct match failed
                    IF exact THEN
                        RETURN false;
                    END IF;
                    
                    -- Check inheritance using _extends table with recursive CTE
                    -- This approach works on read replicas and avoids variable-length edge queries
                    RETURN EXISTS (
                        WITH RECURSIVE model_ancestors AS (
                            -- Base case: start with the child model's internal ID
                            SELECT m.id as internal_id,
                                   trim(both '""' from ag_catalog.agtype_access_operator(m.properties, '""id""'::agtype)::text) as model_name
                            FROM {graphName}.""Model"" m
                            WHERE trim(both '""' from ag_catalog.agtype_access_operator(m.properties, '""id""'::agtype)::text) = twin_model_id
                            
                            UNION ALL
                            
                            -- Recursive case: find parent models through _extends relationships
                            SELECT parent.id as internal_id,
                                   trim(both '""' from ag_catalog.agtype_access_operator(parent.properties, '""id""'::agtype)::text) as model_name
                            FROM model_ancestors ma
                            JOIN {graphName}.""_extends"" e ON e.start_id = ma.internal_id
                            JOIN {graphName}.""Model"" parent ON parent.id = e.end_id
                        )
                        SELECT 1 FROM model_ancestors
                        WHERE model_name = model_id_text
                    );
                END;
                $function$"
            ), */
        ];
    }
}
