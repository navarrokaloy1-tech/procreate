START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017175741_AddRoleLabCategories') THEN
    ALTER TABLE "CustomRoles" ADD "LabCategories" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017175741_AddRoleLabCategories') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017175741_AddRoleLabCategories', '10.0.9');
    END IF;
END $EF$;
COMMIT;

