.timer on
CREATE TABLE inventory(sku INTEGER, warehouse INTEGER, available INTEGER);
WITH RECURSIVE seq(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM seq WHERE n < 200000)
INSERT INTO inventory SELECT n%20000, n/20000, n%20 FROM seq;
EXPLAIN QUERY PLAN SELECT available FROM inventory WHERE sku=123 AND warehouse=4;
SELECT available FROM inventory WHERE sku=123 AND warehouse=4;
CREATE INDEX ix_inventory_sku_warehouse ON inventory(sku,warehouse);
ANALYZE;
EXPLAIN QUERY PLAN SELECT available FROM inventory WHERE sku=123 AND warehouse=4;
SELECT available FROM inventory WHERE sku=123 AND warehouse=4;
