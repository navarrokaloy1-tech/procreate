START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027142050_MergeUltrasoundIntoImaging') THEN
    UPDATE "TestCategories" SET "Department" = 'Imaging' WHERE "Department" = 'Ultrasound';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027142050_MergeUltrasoundIntoImaging') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027142050_MergeUltrasoundIntoImaging', '10.0.9');
    END IF;
END $EF$;
COMMIT;

