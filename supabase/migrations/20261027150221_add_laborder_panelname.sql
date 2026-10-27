START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027150221_AddLabOrderPanelName') THEN
    ALTER TABLE "LabOrders" ADD "PanelName" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027150221_AddLabOrderPanelName') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027150221_AddLabOrderPanelName', '10.0.9');
    END IF;
END $EF$;
COMMIT;

