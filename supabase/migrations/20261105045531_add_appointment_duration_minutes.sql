START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105045531_AddAppointmentDurationMinutes') THEN
    ALTER TABLE "Appointments" ADD "DurationMinutes" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105045531_AddAppointmentDurationMinutes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261105045531_AddAppointmentDurationMinutes', '10.0.9');
    END IF;
END $EF$;
COMMIT;

