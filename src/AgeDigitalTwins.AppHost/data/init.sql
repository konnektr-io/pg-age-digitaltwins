CREATE EXTENSION age;
GRANT SELECT ON ag_catalog.ag_graph TO {{ default "app" .Values.cluster.initdb.owner }};
GRANT USAGE ON SCHEMA ag_catalog TO {{ default "app" .Values.cluster.initdb.owner }};
ALTER USER {{ default "app" .Values.cluster.initdb.owner }} REPLICATION;
CREATE PUBLICATION age_pub FOR ALL TABLES;
SELECT * FROM pg_create_logical_replication_slot('age_slot', 'pgoutput');