START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017133334_AddDoctorAffiliationAndReferral') THEN
    ALTER TABLE "Visits" ADD "ReferringDoctorId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017133334_AddDoctorAffiliationAndReferral') THEN
    ALTER TABLE "Doctors" ADD "Affiliation" text NOT NULL DEFAULT 'In-House';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017133334_AddDoctorAffiliationAndReferral') THEN
    CREATE INDEX "IX_Visits_ReferringDoctorId" ON "Visits" ("ReferringDoctorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017133334_AddDoctorAffiliationAndReferral') THEN
    ALTER TABLE "Visits" ADD CONSTRAINT "FK_Visits_Doctors_ReferringDoctorId" FOREIGN KEY ("ReferringDoctorId") REFERENCES "Doctors" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017133334_AddDoctorAffiliationAndReferral') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017133334_AddDoctorAffiliationAndReferral', '10.0.9');
    END IF;
END $EF$;
COMMIT;

