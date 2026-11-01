START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101080334_AddBatchServiceDurations') THEN
    ALTER TABLE "AppointmentBatchTemplates" ADD "DefaultServiceMinutes" integer NOT NULL DEFAULT 15;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101080334_AddBatchServiceDurations') THEN
    ALTER TABLE "AppointmentBatchTemplates" ADD "ServiceDurationsJson" text NOT NULL DEFAULT '{}';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101080334_AddBatchServiceDurations') THEN
    ALTER TABLE "AppointmentBatches" ADD "DefaultServiceMinutes" integer NOT NULL DEFAULT 15;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101080334_AddBatchServiceDurations') THEN
    ALTER TABLE "AppointmentBatches" ADD "ServiceDurationsJson" text NOT NULL DEFAULT '{}';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101080334_AddBatchServiceDurations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101080334_AddBatchServiceDurations', '10.0.9');
    END IF;
END $EF$;
COMMIT;

