-- Reference data only (lookup rows for reference_entries).
-- Run after: 01_CreateSchema_PostgreSQL.sql
-- Then run: 02_Minimal_AdminAndReference_PostgreSQL.sql (Admin role + user).

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Business Type', 'Startup', 'startup', true, 1),
('Business Type', 'SME', 'sme', true, 2),
('Business Type', 'Enterprise', 'enterprise', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Industry', 'Technology', 'technology', true, 1),
('Industry', 'Finance', 'finance', true, 2),
('Industry', 'Healthcare', 'healthcare', true, 3),
('Industry', 'Manufacturing', 'manufacturing', true, 4),
('Industry', 'Apparel', 'apparel', true, 5),
('Industry', 'Mobile', 'mobile', true, 6),
('Industry', 'Footwear', 'footwear', true, 7),
('Industry', 'Cosmetics', 'cosmetics', true, 8);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('City', 'Mumbai', 'mumbai', true, 1),
('City', 'Bangalore', 'bangalore', true, 2),
('City', 'Delhi', 'delhi', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('State', 'Maharashtra', 'maharashtra', true, 1),
('State', 'Karnataka', 'karnataka', true, 2),
('State', 'Delhi NCR', 'delhi_ncr', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Country', 'India', 'india', true, 1),
('Country', 'USA', 'usa', true, 2);

INSERT INTO reference_entries (category, label, value, is_active, sort_order, requires_implementation) VALUES
('Service Type', 'SaaS', 'saas', true, 1, true),
('Service Type', 'ERP', 'erp', true, 2, true),
('Service Type', 'AMC', 'amc', true, 3, false),
('Service Type', 'Implementation', 'implementation', true, 4, true);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Service', 'ERP License', 'ERP_LICENSE', true, 1),
('Service', 'AMC', 'AMC', true, 2),
('Service', 'Customization', 'CUSTOMIZE', true, 3),
('Service', 'Training', 'TRAINING', true, 4),
('Service', 'Hosting Charges', 'HOSTING', true, 5),
('Service', 'Subscription', 'SAAS', true, 6),
('Service', 'Feature Enabling', 'FEATURES', true, 7),
('Service', 'Implementation', 'IMPLEMENTATION', true, 8),
('Service', 'E-Commerce', 'E-COMMERCE', true, 9);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Shop Size', 'Micro Store (0-2000)', '0-2000', true, 1),
('Shop Size', 'Small Retail (2000-5000)', '2000-5000', true, 2),
('Shop Size', 'Medium Retail (5000-10000)', '5000-10000', true, 3),
('Shop Size', 'Large Retail (10000-30000)', '10000-30000', true, 4),
('Shop Size', 'Mega Store (30000-100000)', '30000-100000', true, 5),
('Shop Size', 'Hypermart Store (100000+)', '100000+', true, 6);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('City Tier', 'Tier I', 'TIER_I', true, 1),
('City Tier', 'Tier II', 'TIER_II', true, 2),
('City Tier', 'Tier III', 'TIER_III', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Service Status', 'Active', 'active', true, 1),
('Service Status', 'On Hold', 'on_hold', true, 2),
('Service Status', 'Completed', 'completed', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Tax', 'GST 18%', 'gst_18', true, 1),
('Tax', 'GST 12%', 'gst_12', true, 2),
('Tax', 'No Tax', 'no_tax', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Frequency', 'Monthly', 'monthly', true, 1),
('Frequency', 'Yearly', 'yearly', true, 2),
('Frequency', 'One-Time', 'one_time', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Payment Frequency', 'Yearly', 'YEARLY', true, 1),
('Payment Frequency', 'Monthly', 'MONTHLY', true, 2),
('Payment Frequency', 'One Time', 'ONE_TIME', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Inventory Value Unit', 'Lakhs', 'LAKH', true, 1),
('Inventory Value Unit', 'Crores', 'CRORE', true, 2);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Payment Mode', 'Bank Transfer', 'bank_transfer', true, 1),
('Payment Mode', 'UPI', 'upi', true, 2),
('Payment Mode', 'Cash', 'cash', true, 3),
('Payment Mode', 'Cheque', 'cheque', true, 4),
('Payment Mode', 'Online Account Transfer', 'ONLINE_ACCOUNT', true, 5),
('Payment Mode', 'UPI (BHIM / Gpay)', 'UPI', true, 6);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Payment Status', 'Paid', 'paid', true, 1),
('Payment Status', 'Pending', 'pending', true, 2),
('Payment Status', 'Overdue', 'overdue', true, 3),
('Payment Status', 'Failed', 'failed', true, 4);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Investment Type', 'Equity', 'equity', true, 1),
('Investment Type', 'Debt', 'debt', true, 2),
('Investment Type', 'Convertible Note', 'convertible_note', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Business Nature', 'Retail Shops', 'RETAIL', true, 1),
('Business Nature', 'Manufacturers', 'MANUFACTURER', true, 2),
('Business Nature', 'Large Format', 'LARGE_FORMAT', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Implementation Stage', 'Discovery', 'discovery', true, 1),
('Implementation Stage', 'Planning', 'planning', true, 2),
('Implementation Stage', 'Execution', 'execution', true, 3),
('Implementation Stage', 'Review', 'review', true, 4),
('Implementation Stage', 'Handover', 'handover', true, 5);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Ticket Category', 'Bug', 'bug', true, 1),
('Ticket Category', 'Feature Request', 'feature_request', true, 2),
('Ticket Category', 'Performance', 'performance', true, 3),
('Ticket Category', 'Billing', 'billing', true, 4);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Lead Source', 'Website', 'website', true, 1),
('Lead Source', 'Referral', 'referral', true, 2),
('Lead Source', 'Webinar', 'webinar', true, 3);

INSERT INTO reference_entries (category, label, value, is_active, sort_order) VALUES
('Customer Type', 'Lead', 'lead', true, 1),
('Customer Type', 'Prospect', 'prospect', true, 2),
('Customer Type', 'Customer', 'customer', true, 3);

SELECT 'Reference data inserted' AS status;
