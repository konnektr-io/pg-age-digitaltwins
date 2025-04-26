CREATE EXTENSION age;
GRANT SELECT ON ag_catalog.ag_graph TO app;
GRANT USAGE ON SCHEMA ag_catalog TO app;
ALTER USER app REPLICATION;
CREATE PUBLICATION age_pub FOR ALL TABLES;
SELECT * FROM pg_create_logical_replication_slot('age_slot', 'pgoutput');