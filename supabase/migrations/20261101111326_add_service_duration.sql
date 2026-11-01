START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101111326_AddServiceDuration') THEN
    ALTER TABLE "Products" ADD "DurationMinutes" integer NOT NULL DEFAULT 15;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101111326_AddServiceDuration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101111326_AddServiceDuration', '10.0.9');
    END IF;
END $EF$;
COMMIT;

