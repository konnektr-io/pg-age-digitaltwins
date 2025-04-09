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
            new(@$"CREATE INDEX twin_gin_idx ON {graphName}.""Twin"" USING gin (properties);"),
            new(@$"ALTER TABLE {graphName}.""Twin"" REPLICA IDENTITY FULL;"),
            new(@$"SELECT create_vlabel('{graphName}', 'Model');"),
            new(
                @$"CREATE UNIQUE INDEX model_id_idx ON {graphName}.""Model"" (ag_catalog.agtype_access_operator(properties, '""id""'::agtype));"
            ),
            new(@$"CREATE INDEX model_gin_idx ON {graphName}.""Model"" USING gin (properties);"),
            new(@$"ALTER TABLE {graphName}.""Model"" REPLICA IDENTITY FULL;"),
            new(@$"SELECT create_elabel('{graphName}', '_extends');"),
            new(@$"ALTER TABLE {graphName}.""_extends"" REPLICA IDENTITY FULL;"),
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_of_model(
                    twin agtype,
                    model_id agtype,
                    strict boolean DEFAULT false
                )
                RETURNS boolean
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    twin_model_id text;
                    result boolean;
                BEGIN
                    SELECT ag_catalog.agtype_access_operator(twin, '""$metadata""'::agtype, '""$model""'::agtype)
                    INTO twin_model_id;

                    IF strict THEN
                        RETURN twin_model_id = model_id::text;
                    ELSE
                        EXECUTE format(
                            $$
                            SELECT EXISTS (
                                SELECT 1
                                FROM ag_catalog.cypher('%s', $$
                                    MATCH (m:Model) - [:_extends*0..]->(n:Model)
                                    WHERE m.id = '%s' AND n.id = '%s'
                                    RETURN m.id
                                $$) AS (m text)
                            )
                            $$,
                            '{graphName}', twin_model_id, model_id
                        )
                        INTO result;

                        RETURN result;
                    END IF;
                END;
                $function$;"
            ),
            /* new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_of_model(twin agtype, model_id agtype, strict boolean default false)
        RETURNS boolean
        LANGUAGE plpgsql
        AS $function$
        DECLARE
            sql VARCHAR;
            twin_model_id text;
            result boolean;
        BEGIN
            SELECT ag_catalog.agtype_access_operator(twin,'""$metadata""'::agtype,'""$model""'::agtype) INTO twin_model_id;
            IF strict THEN
              sql:= format('SELECT ''%s'' = ''%s''', twin_model_id, model_id);
            ELSE
              sql:= format('
                SELECT ''%s'' = ''%s'' OR
                EXISTS
                    (SELECT m FROM ag_catalog.cypher(''{graphName}'', $$
                      MATCH(m:Model) - [:_extends*0..]->(n:Model)
                      WHERE m.id = %s AND n.id = %s
                      RETURN m.id
                    $$) AS(m text))
                ', twin_model_id, model_id, twin_model_id, model_id);
            END IF;
                    EXECUTE sql INTO result;
                    RETURN result;
                    END;
        $function$;"
            ), */
            new(
                @$"CREATE OR REPLACE FUNCTION public.agtype_set(target agtype, path agtype, new_value agtype)
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
                @$"CREATE OR REPLACE FUNCTION public.agtype_delete_key(target agtype, path agtype)
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
