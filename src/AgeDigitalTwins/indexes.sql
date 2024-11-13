
ALTER TABLE digitaltwins."Twin" ADD PRIMARY KEY (_dtId);

CREATE UNIQUE INDEX twin_id_idx ON digitaltwins."Twin"
(ag_catalog.agtype_access_operator(properties, '"_dtId"'::agtype));