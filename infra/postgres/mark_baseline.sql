CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
    "ProductVersion" character varying(32) NOT NULL
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929093646_InitialBaseline', '8.0.11')
ON CONFLICT DO NOTHING;

SELECT * FROM "__EFMigrationsHistory";
