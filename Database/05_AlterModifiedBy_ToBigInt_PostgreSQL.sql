-- Migrate existing databases: modified_by as BIGINT (user id), not VARCHAR.
-- Maps numeric strings, 'System' / 'SystemAdmin' (case-insensitive) to 1; other text -> NULL.
-- Backup first.
-- Uses modified_by::text so this works whether the column is still VARCHAR/TEXT or already BIGINT.

ALTER TABLE users ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE roles ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE customers ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE customer_timelines ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE services ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE invoices ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE invoice_timelines ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE investments ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE investment_timelines ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE implementation_timelines ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE tickets ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE ticket_timelines ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE trademarks ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE locations ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE location_timelines ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE reports ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);
ALTER TABLE scheduler_events ALTER COLUMN modified_by TYPE BIGINT USING (
  CASE
    WHEN modified_by IS NULL OR btrim(modified_by::text) = '' THEN NULL
    WHEN btrim(modified_by::text) ~ '^[0-9]+$' THEN btrim(modified_by::text)::BIGINT
    WHEN lower(btrim(modified_by::text)) IN ('system', 'systemadmin') THEN 1::BIGINT
    ELSE NULL
  END
);

SELECT 'modified_by columns altered to BIGINT.' AS status;
