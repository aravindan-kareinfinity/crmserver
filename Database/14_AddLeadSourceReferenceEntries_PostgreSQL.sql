-- Ensure Lead Source reference entries exist (idempotent).

INSERT INTO reference_entries (category, label, value, is_active, sort_order)
SELECT 'Lead Source', 'External', 'external', true, 1
WHERE NOT EXISTS (SELECT 1 FROM reference_entries WHERE category = 'Lead Source' AND value = 'external');

INSERT INTO reference_entries (category, label, value, is_active, sort_order)
SELECT 'Lead Source', 'Internal', 'internal', true, 2
WHERE NOT EXISTS (SELECT 1 FROM reference_entries WHERE category = 'Lead Source' AND value = 'internal');

INSERT INTO reference_entries (category, label, value, is_active, sort_order)
SELECT 'Lead Source', 'Exhibition / Fair', 'exhibition_fair', true, 3
WHERE NOT EXISTS (SELECT 1 FROM reference_entries WHERE category = 'Lead Source' AND value = 'exhibition_fair');

INSERT INTO reference_entries (category, label, value, is_active, sort_order)
SELECT 'Lead Source', 'Social Media Campaign', 'social_media_campaign', true, 4
WHERE NOT EXISTS (SELECT 1 FROM reference_entries WHERE category = 'Lead Source' AND value = 'social_media_campaign');
