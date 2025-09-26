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
                    twin_model_id text;
                    model_id_text text;
                    result boolean;
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
                    
                    -- Check inheritance using _extends relationships via Cypher
                    SELECT EXISTS (
                        SELECT * FROM ag_catalog.cypher('{graphName}', $$
                            MATCH (child:Model {{id: $child_id}})-[:_extends*0..]->(parent:Model {{id: $parent_id}})
                            RETURN true
                        $$, agtype_build_map('child_id'::text, twin_model_id::agtype, 'parent_id'::text, model_id_text::agtype)
                        ) AS (exists_path agtype)
                    ) INTO result;
                    
                    RETURN COALESCE(result, false);
                END;
                $function$"
            ),
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.agtype_set(target agtype, path agtype, new_value agtype)
                RETURNS agtype AS $$
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
                RETURNS agtype AS $$
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
        ];
    }
}
