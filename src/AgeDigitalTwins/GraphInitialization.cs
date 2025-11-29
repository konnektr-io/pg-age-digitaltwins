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
                PARALLEL SAFE
                AS $function$
                DECLARE
                    twin_model_id agtype;
                    models_array agtype;
                BEGIN
                    twin_model_id := ag_catalog.agtype_access_operator(twin.properties,'""$metadata""','""$model""');
                    
                    -- Compare for direct matches first 
                    IF twin_model_id = target_model_id THEN
                        RETURN true;
                    END IF;

                    -- If exact match requested, return false
                    IF exact THEN
                        RETURN false;
                    END IF;

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
                LANGUAGE plpgsql
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
                LANGUAGE plpgsql
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
                $$"
            ),
            // IS_OBJECT: returns true if agtype is a map/object
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_object(val agtype)
                LANGUAGE plpgsql
                RETURNS boolean 
                PARALLEL SAFE
                AS $$
                BEGIN
                    RETURN ag_catalog.age_keys(val) IS NOT NULL;
                EXCEPTION
                    WHEN others THEN
                        RETURN false;
                END;
                $$"
            ),
            // IS_NUMBER: returns true if agtype is number
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_number(val agtype)
                LANGUAGE plpgsql
                RETURNS boolean 
                PARALLEL SAFE
                AS $$
                BEGIN
                    RETURN (ag_catalog.age_tofloat(val) IS NOT NULL OR ag_catalog.age_tointeger(val) IS NOT NULL) AND NOT (ag_catalog.age_tostring(val) = val);
                EXCEPTION
                    WHEN others THEN
                        RETURN false;
                END;
                $$"
            ),
            // IS_PRIMITIVE: returns true if agtype is string, number, boolean
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_primitive(val agtype)
                LANGUAGE plpgsql
                RETURNS boolean 
                PARALLEL SAFE
                AS $$
                BEGIN
                    RETURN ag_catalog.age_tostring(val) IS NOT NULL OR ag_catalog.age_tofloat(val) IS NOT NULL OR val = true OR val = false;
                EXCEPTION
                    WHEN others THEN
                        RETURN false;
                END;
                $$"
            ),
            // IS_STRING: returns true if agtype is a string
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_string(val agtype)
                LANGUAGE plpgsql
                RETURNS boolean 
                PARALLEL SAFE
                AS $$
                BEGIN
                    RETURN ag_catalog.age_tostring(val) = val;
                EXCEPTION
                    WHEN others THEN
                        RETURN false;
                END;
                $$"
            ),
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_of_model_old(twin agtype, model_id agtype, exact boolean default false)
                RETURNS boolean
                LANGUAGE plpgsql
                STABLE
                PARALLEL SAFE
                AS $function$
                DECLARE
                    sql VARCHAR;
                    twin_model_id agtype;
                    result boolean;
                BEGIN
                    SELECT ag_catalog.agtype_access_operator(twin,'""$metadata""'::agtype,'""$model""'::agtype) INTO twin_model_id;
                    IF exact THEN
                        sql := format('SELECT ''%s'' = ''%s''', twin_model_id, model_id);
                    ELSE
                        sql := format('SELECT ''%s'' = ''%s'' OR
                        EXISTS
                            (SELECT 1 FROM ag_catalog.cypher(''{graphName}'', $$
                                MATCH (m:Model)
                                WHERE m.id = %s AND %s IN m.bases
                                RETURN m.id
                            $$) AS (m text))
                        ', twin_model_id, model_id, twin_model_id, model_id);
                    END IF;
                    EXECUTE sql INTO result;
                    RETURN result;
                END;
                $function$"
            ),
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
            ),
            new(
                @$"CREATE OR REPLACE FUNCTION {graphName}.is_of_model_old3(
                    twin agtype, 
                    target_model_id agtype, 
                    exact boolean DEFAULT false
                )
                RETURNS boolean
                LANGUAGE plpgsql
                STABLE
                PARALLEL SAFE
                AS $function$
                DECLARE
                    -- Keep everything as agtype
                    twin_model_id agtype;
                    model_bases agtype;
                BEGIN
                    -------------------------------------------------------------------------
                    -- 1. NATIVE EXTRACTION
                    -------------------------------------------------------------------------
                    -- Use the -> operator to drill down into the map.
                    -- Note: keys must be valid agtype strings (e.g. '""$metadata""'::agtype)
                    
                    twin_model_id := ag_catalog.agtype_access_operator(twin.properties,'""$metadata""','""$model""');

                    -------------------------------------------------------------------------
                    -- 2. DIRECT MATCH
                    -------------------------------------------------------------------------
                    -- Compare agtype directly. 
                    -- This works because both are agtype strings (e.g., '""Car""'::agtype)
                    IF twin_model_id = target_model_id THEN
                        RETURN true;
                    END IF;

                    -- Fail fast if exact match was requested or if twin has no model
                    IF exact OR twin_model_id IS NULL THEN
                        RETURN false;
                    END IF;

                    -------------------------------------------------------------------------
                    -- 3. OPTIMIZED BASES/INHERITANCE CHECK (The STABLE Cache Point)
                    -------------------------------------------------------------------------
                    -- This entire SELECT block is calculated once per statement for a given target_model_id.
                    -- We collect all Model IDs that should satisfy the ""is_of_model"" check.
                    SELECT 
                        ag_catalog.agtype_array_agg(model_id_prop)
                    INTO descendant_models_array
                    FROM (
                        -- 1. Find all models whose bases array contains the target (The true descendants)
                        SELECT properties -> '""id""'::agtype AS model_id_prop
                        FROM {graphName}.""Model""
                        -- Check if the target is contained in the twin's model's bases
                        WHERE (properties -> '""bases""'::agtype) @> ag_catalog.agtype_build_list(target_model_id)
                        
                        UNION ALL
                        
                        -- 2. Add the target model itself to the list (since we already checked exact match, this
                        -- primarily covers the case where the twin IS the target model, ensuring the
                        -- final check is comprehensive if the direct match was skipped for some reason)
                        SELECT target_model_id AS model_id_prop
                    ) AS aggregated_models;
                    
                    -- Now, check if the twin's model ID is present in this pre-calculated array.
                    -- This is a highly optimized AGTYPE array containment check.
                    RETURN descendant_models_array @> ag_catalog.agtype_build_list(twin_model_id);

                END;
                $function$;"
            ),
        ];
    }
}
