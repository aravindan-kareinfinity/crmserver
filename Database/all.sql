--
-- PostgreSQL database dump
--



-- Dumped from database version 18.3
-- Dumped by pg_dump version 18.3

-- Started on 2026-04-01 18:32:45

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 893 (class 1247 OID 16386)
-- Name: customer_type; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.customer_type AS ENUM (
    'lead',
    'prospect',
    'customer'
);


ALTER TYPE public.customer_type OWNER TO postgres;

--
-- TOC entry 896 (class 1247 OID 16394)
-- Name: implementation_status_enum; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.implementation_status_enum AS ENUM (
    'OPEN',
    'IN_PROGRESS',
    'COMPLETED'
);


ALTER TYPE public.implementation_status_enum OWNER TO postgres;

--
-- TOC entry 899 (class 1247 OID 16402)
-- Name: payment_status_enum; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.payment_status_enum AS ENUM (
    'active',
    'inactive'
);


ALTER TYPE public.payment_status_enum OWNER TO postgres;

--
-- TOC entry 902 (class 1247 OID 16408)
-- Name: ticket_priority; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.ticket_priority AS ENUM (
    'critical',
    'high',
    'medium',
    'low'
);


ALTER TYPE public.ticket_priority OWNER TO postgres;

--
-- TOC entry 905 (class 1247 OID 16418)
-- Name: ticket_status; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.ticket_status AS ENUM (
    'open',
    'in_progress',
    'waiting',
    'resolved',
    'closed'
);


ALTER TYPE public.ticket_status OWNER TO postgres;

--
-- TOC entry 908 (class 1247 OID 16430)
-- Name: trademark_status; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.trademark_status AS ENUM (
    'active',
    'expired',
    'pending',
    'rejected'
);


ALTER TYPE public.trademark_status OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 219 (class 1259 OID 16439)
-- Name: customer_timelines; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.customer_timelines (
    id integer NOT NULL,
    type integer NOT NULL,
    notes text NOT NULL,
    file_id integer,
    file_name character varying(255),
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint NOT NULL,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    customer_code character varying(100) NOT NULL
);


ALTER TABLE public.customer_timelines OWNER TO postgres;

--
-- TOC entry 220 (class 1259 OID 16455)
-- Name: customer_timelines_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.customer_timelines_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.customer_timelines_id_seq OWNER TO postgres;

--
-- TOC entry 5383 (class 0 OID 0)
-- Dependencies: 220
-- Name: customer_timelines_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.customer_timelines_id_seq OWNED BY public.customer_timelines.id;


--
-- TOC entry 221 (class 1259 OID 16456)
-- Name: customers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.customers (
    id integer NOT NULL,
    code character varying(100) NOT NULL,
    reg_name character varying(255) NOT NULL,
    mobile character varying(10) NOT NULL,
    email character varying(255) NOT NULL,
    business_type_id integer,
    industry_id integer,
    address_line1 character varying(255) NOT NULL,
    address_line2 character varying(255),
    city_id integer,
    state_id integer,
    country_id integer,
    pincode character varying(6) NOT NULL,
    gst_number character varying(15),
    contact_persons text,
    emails text,
    mobiles text,
    shop_size_id integer NOT NULL,
    tier_id integer NOT NULL,
    type_id integer NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    total_locations integer,
    total_trade_names integer,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    converted_at timestamp without time zone,
    converted_by character varying(255),
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    pipeline_status character varying(80),
    product_features_discussed boolean DEFAULT false NOT NULL,
    assigned_representative_id bigint,
    interaction_mode_id integer,
    price_plan_selected boolean DEFAULT false NOT NULL,
    quotation_prepared_sent boolean DEFAULT false NOT NULL,
    quotation_accepted boolean DEFAULT false NOT NULL,
    advance_payment_received boolean DEFAULT false NOT NULL,
    invoice_generated boolean DEFAULT false NOT NULL,
    invoice_number character varying(80),
    prospect_converted_at timestamp without time zone,
    prospect_converted_by bigint,
    customer_converted_at timestamp without time zone,
    customer_converted_by bigint,
    lead_source_id integer
);


ALTER TABLE public.customers OWNER TO postgres;

--
-- TOC entry 222 (class 1259 OID 16476)
-- Name: customers_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.customers_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.customers_id_seq OWNER TO postgres;

--
-- TOC entry 5384 (class 0 OID 0)
-- Dependencies: 222
-- Name: customers_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.customers_id_seq OWNED BY public.customers.id;


--
-- TOC entry 223 (class 1259 OID 16477)
-- Name: files; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.files (
    id bigint NOT NULL,
    is_factory boolean DEFAULT false NOT NULL,
    content bytea NOT NULL,
    version integer DEFAULT 1 NOT NULL,
    created_by bigint,
    created_on timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    modified_on timestamp without time zone,
    attributes jsonb,
    is_active boolean DEFAULT true NOT NULL,
    is_suspended boolean DEFAULT false NOT NULL,
    parent_id bigint,
    notes character varying(255),
    type character varying(100)
);


ALTER TABLE public.files OWNER TO postgres;

--
-- TOC entry 224 (class 1259 OID 16494)
-- Name: files_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.files_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.files_id_seq OWNER TO postgres;

--
-- TOC entry 5385 (class 0 OID 0)
-- Dependencies: 224
-- Name: files_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.files_id_seq OWNED BY public.files.id;


--
-- TOC entry 225 (class 1259 OID 16495)
-- Name: implementation_assignments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.implementation_assignments (
    id integer NOT NULL,
    service_id integer NOT NULL,
    user_ids text NOT NULL
);


ALTER TABLE public.implementation_assignments OWNER TO postgres;

--
-- TOC entry 226 (class 1259 OID 16503)
-- Name: implementation_assignments_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.implementation_assignments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.implementation_assignments_id_seq OWNER TO postgres;

--
-- TOC entry 5386 (class 0 OID 0)
-- Dependencies: 226
-- Name: implementation_assignments_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.implementation_assignments_id_seq OWNED BY public.implementation_assignments.id;


--
-- TOC entry 227 (class 1259 OID 16504)
-- Name: implementation_timelines; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.implementation_timelines (
    id integer NOT NULL,
    service_id integer NOT NULL,
    type integer NOT NULL,
    status public.implementation_status_enum NOT NULL,
    notes text NOT NULL,
    file_id integer,
    file_name character varying(255),
    user_id integer NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint NOT NULL,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint
);


ALTER TABLE public.implementation_timelines OWNER TO postgres;

--
-- TOC entry 228 (class 1259 OID 16522)
-- Name: implementation_timelines_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.implementation_timelines_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.implementation_timelines_id_seq OWNER TO postgres;

--
-- TOC entry 5387 (class 0 OID 0)
-- Dependencies: 228
-- Name: implementation_timelines_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.implementation_timelines_id_seq OWNED BY public.implementation_timelines.id;


--
-- TOC entry 229 (class 1259 OID 16523)
-- Name: investment_timelines; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.investment_timelines (
    id integer NOT NULL,
    investment_id integer NOT NULL,
    type integer NOT NULL,
    notes text NOT NULL,
    file_id integer,
    file_name character varying(255),
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint NOT NULL,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint
);


ALTER TABLE public.investment_timelines OWNER TO postgres;

--
-- TOC entry 230 (class 1259 OID 16539)
-- Name: investment_timelines_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.investment_timelines_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.investment_timelines_id_seq OWNER TO postgres;

--
-- TOC entry 5388 (class 0 OID 0)
-- Dependencies: 230
-- Name: investment_timelines_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.investment_timelines_id_seq OWNED BY public.investment_timelines.id;


--
-- TOC entry 231 (class 1259 OID 16540)
-- Name: investments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.investments (
    id integer NOT NULL,
    location_id integer NOT NULL,
    amount numeric(18,2) NOT NULL,
    investment_type_id integer NOT NULL,
    staff_id integer,
    notes text NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    customer_code character varying(100) NOT NULL,
    claimed_amount numeric(12,2) DEFAULT 0 NOT NULL,
    remaining_amount numeric(12,2) DEFAULT 0 NOT NULL,
    claimed_fully boolean DEFAULT false NOT NULL,
    claimed_at timestamp without time zone,
    claimed_by bigint,
    claim_notes character varying(500),
    needs_claim boolean DEFAULT true NOT NULL
);


ALTER TABLE public.investments OWNER TO postgres;

--
-- TOC entry 232 (class 1259 OID 16557)
-- Name: investments_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.investments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.investments_id_seq OWNER TO postgres;

--
-- TOC entry 5389 (class 0 OID 0)
-- Dependencies: 232
-- Name: investments_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.investments_id_seq OWNED BY public.investments.id;


--
-- TOC entry 233 (class 1259 OID 16558)
-- Name: invoice_timelines; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.invoice_timelines (
    id integer NOT NULL,
    invoice_id integer NOT NULL,
    type integer NOT NULL,
    notes text NOT NULL,
    file_id integer,
    file_name character varying(255),
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint NOT NULL,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint
);


ALTER TABLE public.invoice_timelines OWNER TO postgres;

--
-- TOC entry 234 (class 1259 OID 16574)
-- Name: invoice_timelines_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.invoice_timelines_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.invoice_timelines_id_seq OWNER TO postgres;

--
-- TOC entry 5390 (class 0 OID 0)
-- Dependencies: 234
-- Name: invoice_timelines_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.invoice_timelines_id_seq OWNED BY public.invoice_timelines.id;


--
-- TOC entry 235 (class 1259 OID 16575)
-- Name: invoices; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.invoices (
    id integer NOT NULL,
    invoice_number character varying(100) NOT NULL,
    service_id integer NOT NULL,
    staff_id integer,
    payment_mode_id integer NOT NULL,
    payment_status_id integer NOT NULL,
    receivable numeric(18,2) NOT NULL,
    received numeric(18,2) DEFAULT 0 NOT NULL,
    subscription_start_at timestamp without time zone,
    subscription_end_at timestamp without time zone,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    paid_at timestamp without time zone,
    paid_by character varying(255),
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    customer_code character varying(100) NOT NULL
);


ALTER TABLE public.invoices OWNER TO postgres;

--
-- TOC entry 236 (class 1259 OID 16595)
-- Name: invoices_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.invoices_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.invoices_id_seq OWNER TO postgres;

--
-- TOC entry 5391 (class 0 OID 0)
-- Dependencies: 236
-- Name: invoices_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.invoices_id_seq OWNED BY public.invoices.id;


--
-- TOC entry 237 (class 1259 OID 16596)
-- Name: location_timelines; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.location_timelines (
    id integer NOT NULL,
    location_id integer NOT NULL,
    type integer NOT NULL,
    notes text NOT NULL,
    file_id integer,
    file_name character varying(255),
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint NOT NULL,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint
);


ALTER TABLE public.location_timelines OWNER TO postgres;

--
-- TOC entry 238 (class 1259 OID 16612)
-- Name: location_timelines_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.location_timelines_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.location_timelines_id_seq OWNER TO postgres;

--
-- TOC entry 5392 (class 0 OID 0)
-- Dependencies: 238
-- Name: location_timelines_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.location_timelines_id_seq OWNED BY public.location_timelines.id;


--
-- TOC entry 239 (class 1259 OID 16613)
-- Name: locations; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.locations (
    id integer NOT NULL,
    code character varying(100) NOT NULL,
    name character varying(255) NOT NULL,
    reg_name character varying(255) NOT NULL,
    pincode character varying(6) NOT NULL,
    city_id integer NOT NULL,
    state_id integer NOT NULL,
    country_id integer NOT NULL,
    address_line1 character varying(255) NOT NULL,
    address_line2 character varying(255) NOT NULL,
    contact_persons text NOT NULL,
    emails text NOT NULL,
    mobiles text NOT NULL,
    shop_size_id integer NOT NULL,
    tier_id integer NOT NULL,
    is_primary boolean DEFAULT false NOT NULL,
    gst_number character varying(15) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    customer_code character varying(100) NOT NULL
);


ALTER TABLE public.locations OWNER TO postgres;

--
-- TOC entry 240 (class 1259 OID 16643)
-- Name: locations_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.locations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.locations_id_seq OWNER TO postgres;

--
-- TOC entry 5393 (class 0 OID 0)
-- Dependencies: 240
-- Name: locations_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.locations_id_seq OWNED BY public.locations.id;


--
-- TOC entry 260 (class 1259 OID 17227)
-- Name: payments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.payments (
    id integer NOT NULL,
    invoice_id integer NOT NULL,
    customer_code character varying(100) NOT NULL,
    amount numeric(12,2) DEFAULT 0 NOT NULL,
    remaining numeric(12,2) DEFAULT 0 NOT NULL,
    payment_mode_id integer NOT NULL,
    received_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    notes character varying(500),
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint
);


ALTER TABLE public.payments OWNER TO postgres;

--
-- TOC entry 259 (class 1259 OID 17226)
-- Name: payments_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.payments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.payments_id_seq OWNER TO postgres;

--
-- TOC entry 5394 (class 0 OID 0)
-- Dependencies: 259
-- Name: payments_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.payments_id_seq OWNED BY public.payments.id;


--
-- TOC entry 241 (class 1259 OID 16644)
-- Name: reference_entries; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.reference_entries (
    id integer NOT NULL,
    category character varying(100) NOT NULL,
    label character varying(200) NOT NULL,
    value character varying(100) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL,
    requires_implementation boolean,
    is_implementation boolean
);


ALTER TABLE public.reference_entries OWNER TO postgres;

--
-- TOC entry 242 (class 1259 OID 16655)
-- Name: reference_entries_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.reference_entries_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.reference_entries_id_seq OWNER TO postgres;

--
-- TOC entry 5395 (class 0 OID 0)
-- Dependencies: 242
-- Name: reference_entries_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.reference_entries_id_seq OWNED BY public.reference_entries.id;


--
-- TOC entry 243 (class 1259 OID 16656)
-- Name: reports; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.reports (
    id integer NOT NULL,
    name character varying(255) NOT NULL,
    module character varying(100) NOT NULL,
    columns text NOT NULL,
    filters text NOT NULL,
    group_by character varying(100),
    sort_by character varying(100),
    is_active boolean DEFAULT true NOT NULL,
    created_by bigint NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    last_run timestamp without time zone NOT NULL,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    query text
);


ALTER TABLE public.reports OWNER TO postgres;

--
-- TOC entry 244 (class 1259 OID 16674)
-- Name: reports_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.reports_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.reports_id_seq OWNER TO postgres;

--
-- TOC entry 5396 (class 0 OID 0)
-- Dependencies: 244
-- Name: reports_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.reports_id_seq OWNED BY public.reports.id;


--
-- TOC entry 245 (class 1259 OID 16675)
-- Name: roles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.roles (
    id integer NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500) NOT NULL,
    permissions text NOT NULL,
    user_count integer,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint
);


ALTER TABLE public.roles OWNER TO postgres;

--
-- TOC entry 246 (class 1259 OID 16688)
-- Name: roles_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.roles_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.roles_id_seq OWNER TO postgres;

--
-- TOC entry 5397 (class 0 OID 0)
-- Dependencies: 246
-- Name: roles_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.roles_id_seq OWNED BY public.roles.id;


--
-- TOC entry 247 (class 1259 OID 16689)
-- Name: scheduler_events; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.scheduler_events (
    id integer NOT NULL,
    title character varying(255) NOT NULL,
    description text NOT NULL,
    start_time timestamp without time zone NOT NULL,
    end_time timestamp without time zone NOT NULL,
    attendees text NOT NULL,
    location character varying(255),
    type character varying(50) NOT NULL,
    priority character varying(50) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    related_to_type character varying(50),
    related_to_id integer,
    created_by bigint NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    status character varying(50) DEFAULT 'scheduled'::character varying NOT NULL
);


ALTER TABLE public.scheduler_events OWNER TO postgres;

--
-- TOC entry 248 (class 1259 OID 16711)
-- Name: scheduler_events_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.scheduler_events_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.scheduler_events_id_seq OWNER TO postgres;

--
-- TOC entry 5398 (class 0 OID 0)
-- Dependencies: 248
-- Name: scheduler_events_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.scheduler_events_id_seq OWNED BY public.scheduler_events.id;


--
-- TOC entry 249 (class 1259 OID 16712)
-- Name: services; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.services (
    id integer NOT NULL,
    location_id integer,
    trade_name_id integer,
    service_type_id integer NOT NULL,
    frequency_id integer,
    live_date timestamp without time zone,
    service_value numeric(18,2),
    due_month integer NOT NULL,
    implementation_required boolean DEFAULT false NOT NULL,
    implementation_stage_id integer,
    implementation_started_at timestamp without time zone,
    implementation_started_by character varying(255),
    implementation_completed_at timestamp without time zone,
    implementation_completed_by character varying(255),
    project_title character varying(255),
    project_manager_id integer,
    budget_amount numeric(18,2),
    progress_percentage integer,
    tax_id integer,
    notes text,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    implementation_status public.implementation_status_enum DEFAULT 'OPEN'::public.implementation_status_enum NOT NULL,
    customer_code character varying(100) NOT NULL,
    due_date timestamp without time zone CONSTRAINT services_due_date_ts_not_null NOT NULL,
    amc_percentage numeric(6,2),
    amc_amount numeric(12,2)
);


ALTER TABLE public.services OWNER TO postgres;

--
-- TOC entry 250 (class 1259 OID 16732)
-- Name: services_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.services_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.services_id_seq OWNER TO postgres;

--
-- TOC entry 5399 (class 0 OID 0)
-- Dependencies: 250
-- Name: services_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.services_id_seq OWNED BY public.services.id;


--
-- TOC entry 251 (class 1259 OID 16733)
-- Name: ticket_timelines; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.ticket_timelines (
    id integer NOT NULL,
    ticket_id integer NOT NULL,
    type integer NOT NULL,
    notes text NOT NULL,
    file_id integer,
    file_name character varying(255),
    user_id integer NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint NOT NULL,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint
);


ALTER TABLE public.ticket_timelines OWNER TO postgres;

--
-- TOC entry 252 (class 1259 OID 16750)
-- Name: ticket_timelines_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.ticket_timelines_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.ticket_timelines_id_seq OWNER TO postgres;

--
-- TOC entry 5400 (class 0 OID 0)
-- Dependencies: 252
-- Name: ticket_timelines_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.ticket_timelines_id_seq OWNED BY public.ticket_timelines.id;


--
-- TOC entry 253 (class 1259 OID 16751)
-- Name: tickets; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.tickets (
    id integer NOT NULL,
    location_id integer NOT NULL,
    subject character varying(500) NOT NULL,
    description text NOT NULL,
    status public.ticket_status DEFAULT 'open'::public.ticket_status NOT NULL,
    priority public.ticket_priority DEFAULT 'medium'::public.ticket_priority NOT NULL,
    assigned_to integer NOT NULL,
    sla_deadline timestamp without time zone NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    closed_at timestamp without time zone,
    closed_by bigint,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    category character varying(100) NOT NULL,
    module character varying(100),
    customer_code character varying(100) NOT NULL,
    contact_person character varying(255),
    contact_mobile character varying(20)
);


ALTER TABLE public.tickets OWNER TO postgres;

--
-- TOC entry 254 (class 1259 OID 16774)
-- Name: tickets_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.tickets_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.tickets_id_seq OWNER TO postgres;

--
-- TOC entry 5401 (class 0 OID 0)
-- Dependencies: 254
-- Name: tickets_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.tickets_id_seq OWNED BY public.tickets.id;


--
-- TOC entry 255 (class 1259 OID 16775)
-- Name: trademarks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.trademarks (
    id integer NOT NULL,
    location_id integer NOT NULL,
    reg_name character varying(255) NOT NULL,
    gst_number character varying(15) NOT NULL,
    pincode character varying(6) NOT NULL,
    city_id integer NOT NULL,
    state_id integer NOT NULL,
    country_id integer,
    address_line1 character varying(255) NOT NULL,
    address_line2 character varying(255),
    contact_persons text NOT NULL,
    emails text NOT NULL,
    mobiles text NOT NULL,
    tier_id integer NOT NULL,
    shop_size_id integer,
    registration_number character varying(100),
    category character varying(255),
    description text,
    registration_date timestamp without time zone,
    expiry_date timestamp without time zone,
    is_active boolean DEFAULT true NOT NULL,
    remarks text,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    customer_code character varying(100) NOT NULL
);


ALTER TABLE public.trademarks OWNER TO postgres;

--
-- TOC entry 256 (class 1259 OID 16799)
-- Name: trademarks_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.trademarks_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.trademarks_id_seq OWNER TO postgres;

--
-- TOC entry 5402 (class 0 OID 0)
-- Dependencies: 256
-- Name: trademarks_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.trademarks_id_seq OWNED BY public.trademarks.id;


--
-- TOC entry 257 (class 1259 OID 16800)
-- Name: users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users (
    id integer NOT NULL,
    user_id character varying(100) NOT NULL,
    first_name character varying(255) NOT NULL,
    last_name character varying(255) NOT NULL,
    email character varying(255) NOT NULL,
    role character varying(100) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    last_login timestamp without time zone NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by bigint,
    modified_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modified_by bigint,
    password_hash character varying(512) NOT NULL
);


ALTER TABLE public.users OWNER TO postgres;

--
-- TOC entry 258 (class 1259 OID 16819)
-- Name: users_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.users_id_seq OWNER TO postgres;

--
-- TOC entry 5403 (class 0 OID 0)
-- Dependencies: 258
-- Name: users_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;


--
-- TOC entry 4974 (class 2604 OID 16820)
-- Name: customer_timelines id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customer_timelines ALTER COLUMN id SET DEFAULT nextval('public.customer_timelines_id_seq'::regclass);


--
-- TOC entry 4978 (class 2604 OID 16821)
-- Name: customers id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers ALTER COLUMN id SET DEFAULT nextval('public.customers_id_seq'::regclass);


--
-- TOC entry 4988 (class 2604 OID 16822)
-- Name: files id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.files ALTER COLUMN id SET DEFAULT nextval('public.files_id_seq'::regclass);


--
-- TOC entry 4994 (class 2604 OID 16823)
-- Name: implementation_assignments id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.implementation_assignments ALTER COLUMN id SET DEFAULT nextval('public.implementation_assignments_id_seq'::regclass);


--
-- TOC entry 4995 (class 2604 OID 16824)
-- Name: implementation_timelines id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.implementation_timelines ALTER COLUMN id SET DEFAULT nextval('public.implementation_timelines_id_seq'::regclass);


--
-- TOC entry 4999 (class 2604 OID 16825)
-- Name: investment_timelines id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.investment_timelines ALTER COLUMN id SET DEFAULT nextval('public.investment_timelines_id_seq'::regclass);


--
-- TOC entry 5003 (class 2604 OID 16826)
-- Name: investments id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.investments ALTER COLUMN id SET DEFAULT nextval('public.investments_id_seq'::regclass);


--
-- TOC entry 5011 (class 2604 OID 16827)
-- Name: invoice_timelines id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoice_timelines ALTER COLUMN id SET DEFAULT nextval('public.invoice_timelines_id_seq'::regclass);


--
-- TOC entry 5015 (class 2604 OID 16828)
-- Name: invoices id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoices ALTER COLUMN id SET DEFAULT nextval('public.invoices_id_seq'::regclass);


--
-- TOC entry 5020 (class 2604 OID 16829)
-- Name: location_timelines id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.location_timelines ALTER COLUMN id SET DEFAULT nextval('public.location_timelines_id_seq'::regclass);


--
-- TOC entry 5024 (class 2604 OID 16830)
-- Name: locations id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.locations ALTER COLUMN id SET DEFAULT nextval('public.locations_id_seq'::regclass);


--
-- TOC entry 5068 (class 2604 OID 17230)
-- Name: payments id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payments ALTER COLUMN id SET DEFAULT nextval('public.payments_id_seq'::regclass);


--
-- TOC entry 5029 (class 2604 OID 16831)
-- Name: reference_entries id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.reference_entries ALTER COLUMN id SET DEFAULT nextval('public.reference_entries_id_seq'::regclass);


--
-- TOC entry 5032 (class 2604 OID 16832)
-- Name: reports id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.reports ALTER COLUMN id SET DEFAULT nextval('public.reports_id_seq'::regclass);


--
-- TOC entry 5036 (class 2604 OID 16833)
-- Name: roles id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles ALTER COLUMN id SET DEFAULT nextval('public.roles_id_seq'::regclass);


--
-- TOC entry 5039 (class 2604 OID 16834)
-- Name: scheduler_events id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.scheduler_events ALTER COLUMN id SET DEFAULT nextval('public.scheduler_events_id_seq'::regclass);


--
-- TOC entry 5044 (class 2604 OID 16835)
-- Name: services id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.services ALTER COLUMN id SET DEFAULT nextval('public.services_id_seq'::regclass);


--
-- TOC entry 5050 (class 2604 OID 16836)
-- Name: ticket_timelines id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.ticket_timelines ALTER COLUMN id SET DEFAULT nextval('public.ticket_timelines_id_seq'::regclass);


--
-- TOC entry 5054 (class 2604 OID 16837)
-- Name: tickets id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tickets ALTER COLUMN id SET DEFAULT nextval('public.tickets_id_seq'::regclass);


--
-- TOC entry 5060 (class 2604 OID 16838)
-- Name: trademarks id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trademarks ALTER COLUMN id SET DEFAULT nextval('public.trademarks_id_seq'::regclass);


--
-- TOC entry 5064 (class 2604 OID 16839)
-- Name: users id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);


--
-- TOC entry 5076 (class 2606 OID 16841)
-- Name: customer_timelines customer_timelines_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customer_timelines
    ADD CONSTRAINT customer_timelines_pkey PRIMARY KEY (id);


--
-- TOC entry 5080 (class 2606 OID 16843)
-- Name: customers customers_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_pkey PRIMARY KEY (id);


--
-- TOC entry 5092 (class 2606 OID 16845)
-- Name: files files_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.files
    ADD CONSTRAINT files_pkey PRIMARY KEY (id);


--
-- TOC entry 5097 (class 2606 OID 16847)
-- Name: implementation_assignments implementation_assignments_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.implementation_assignments
    ADD CONSTRAINT implementation_assignments_pkey PRIMARY KEY (id);


--
-- TOC entry 5101 (class 2606 OID 16849)
-- Name: implementation_timelines implementation_timelines_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.implementation_timelines
    ADD CONSTRAINT implementation_timelines_pkey PRIMARY KEY (id);


--
-- TOC entry 5104 (class 2606 OID 16851)
-- Name: investment_timelines investment_timelines_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.investment_timelines
    ADD CONSTRAINT investment_timelines_pkey PRIMARY KEY (id);


--
-- TOC entry 5108 (class 2606 OID 16853)
-- Name: investments investments_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.investments
    ADD CONSTRAINT investments_pkey PRIMARY KEY (id);


--
-- TOC entry 5111 (class 2606 OID 16855)
-- Name: invoice_timelines invoice_timelines_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoice_timelines
    ADD CONSTRAINT invoice_timelines_pkey PRIMARY KEY (id);


--
-- TOC entry 5117 (class 2606 OID 16857)
-- Name: invoices invoices_invoice_number_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoices
    ADD CONSTRAINT invoices_invoice_number_key UNIQUE (invoice_number);


--
-- TOC entry 5119 (class 2606 OID 16859)
-- Name: invoices invoices_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoices
    ADD CONSTRAINT invoices_pkey PRIMARY KEY (id);


--
-- TOC entry 5122 (class 2606 OID 16861)
-- Name: location_timelines location_timelines_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.location_timelines
    ADD CONSTRAINT location_timelines_pkey PRIMARY KEY (id);


--
-- TOC entry 5127 (class 2606 OID 16863)
-- Name: locations locations_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.locations
    ADD CONSTRAINT locations_pkey PRIMARY KEY (id);


--
-- TOC entry 5179 (class 2606 OID 17250)
-- Name: payments payments_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payments
    ADD CONSTRAINT payments_pkey PRIMARY KEY (id);


--
-- TOC entry 5131 (class 2606 OID 16865)
-- Name: reference_entries reference_entries_category_value_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.reference_entries
    ADD CONSTRAINT reference_entries_category_value_key UNIQUE (category, value);


--
-- TOC entry 5133 (class 2606 OID 16867)
-- Name: reference_entries reference_entries_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.reference_entries
    ADD CONSTRAINT reference_entries_pkey PRIMARY KEY (id);


--
-- TOC entry 5136 (class 2606 OID 16869)
-- Name: reports reports_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.reports
    ADD CONSTRAINT reports_pkey PRIMARY KEY (id);


--
-- TOC entry 5138 (class 2606 OID 16871)
-- Name: roles roles_name_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_name_key UNIQUE (name);


--
-- TOC entry 5140 (class 2606 OID 16873)
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id);


--
-- TOC entry 5145 (class 2606 OID 16875)
-- Name: scheduler_events scheduler_events_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.scheduler_events
    ADD CONSTRAINT scheduler_events_pkey PRIMARY KEY (id);


--
-- TOC entry 5151 (class 2606 OID 16877)
-- Name: services services_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.services
    ADD CONSTRAINT services_pkey PRIMARY KEY (id);


--
-- TOC entry 5154 (class 2606 OID 16879)
-- Name: ticket_timelines ticket_timelines_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.ticket_timelines
    ADD CONSTRAINT ticket_timelines_pkey PRIMARY KEY (id);


--
-- TOC entry 5162 (class 2606 OID 16881)
-- Name: tickets tickets_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tickets
    ADD CONSTRAINT tickets_pkey PRIMARY KEY (id);


--
-- TOC entry 5166 (class 2606 OID 16883)
-- Name: trademarks trademarks_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trademarks
    ADD CONSTRAINT trademarks_pkey PRIMARY KEY (id);


--
-- TOC entry 5170 (class 2606 OID 16885)
-- Name: users users_email_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_email_key UNIQUE (email);


--
-- TOC entry 5172 (class 2606 OID 16887)
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- TOC entry 5174 (class 2606 OID 16889)
-- Name: users users_user_id_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_user_id_key UNIQUE (user_id);


--
-- TOC entry 5078 (class 1259 OID 17152)
-- Name: customers_code_key; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX customers_code_key ON public.customers USING btree (code);


--
-- TOC entry 5081 (class 1259 OID 16890)
-- Name: idx_customer_created_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customer_created_at ON public.customers USING btree (created_at);


--
-- TOC entry 5082 (class 1259 OID 16891)
-- Name: idx_customer_email; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customer_email ON public.customers USING btree (email);


--
-- TOC entry 5083 (class 1259 OID 16892)
-- Name: idx_customer_is_active; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customer_is_active ON public.customers USING btree (is_active);


--
-- TOC entry 5084 (class 1259 OID 17296)
-- Name: idx_customer_lead_source_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customer_lead_source_id ON public.customers USING btree (lead_source_id);


--
-- TOC entry 5077 (class 1259 OID 17160)
-- Name: idx_customer_timeline_customer_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customer_timeline_customer_code ON public.customer_timelines USING btree (customer_code);


--
-- TOC entry 5085 (class 1259 OID 16894)
-- Name: idx_customer_type_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customer_type_id ON public.customers USING btree (type_id);


--
-- TOC entry 5086 (class 1259 OID 17224)
-- Name: idx_customers_assigned_representative_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customers_assigned_representative_id ON public.customers USING btree (assigned_representative_id);


--
-- TOC entry 5087 (class 1259 OID 17151)
-- Name: idx_customers_code_unique; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX idx_customers_code_unique ON public.customers USING btree (code) WHERE ((code IS NOT NULL) AND (TRIM(BOTH FROM code) <> ''::text));


--
-- TOC entry 5088 (class 1259 OID 17286)
-- Name: idx_customers_customer_converted_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customers_customer_converted_at ON public.customers USING btree (customer_converted_at);


--
-- TOC entry 5089 (class 1259 OID 17225)
-- Name: idx_customers_interaction_mode_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customers_interaction_mode_id ON public.customers USING btree (interaction_mode_id);


--
-- TOC entry 5090 (class 1259 OID 17285)
-- Name: idx_customers_prospect_converted_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customers_prospect_converted_at ON public.customers USING btree (prospect_converted_at);


--
-- TOC entry 5093 (class 1259 OID 16895)
-- Name: idx_files_is_active; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_files_is_active ON public.files USING btree (is_active);


--
-- TOC entry 5094 (class 1259 OID 16896)
-- Name: idx_files_parent_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_files_parent_id ON public.files USING btree (parent_id);


--
-- TOC entry 5095 (class 1259 OID 16897)
-- Name: idx_implementation_assignment_service_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_implementation_assignment_service_id ON public.implementation_assignments USING btree (service_id);


--
-- TOC entry 5098 (class 1259 OID 16898)
-- Name: idx_implementation_timeline_service_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_implementation_timeline_service_id ON public.implementation_timelines USING btree (service_id);


--
-- TOC entry 5099 (class 1259 OID 16899)
-- Name: idx_implementation_timeline_status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_implementation_timeline_status ON public.implementation_timelines USING btree (status);


--
-- TOC entry 5105 (class 1259 OID 17181)
-- Name: idx_investment_customer_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_investment_customer_code ON public.investments USING btree (customer_code);


--
-- TOC entry 5102 (class 1259 OID 16901)
-- Name: idx_investment_timeline_investment_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_investment_timeline_investment_id ON public.investment_timelines USING btree (investment_id);


--
-- TOC entry 5106 (class 1259 OID 17289)
-- Name: idx_investments_needs_claim; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_investments_needs_claim ON public.investments USING btree (needs_claim);


--
-- TOC entry 5112 (class 1259 OID 16902)
-- Name: idx_invoice_created_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_invoice_created_at ON public.invoices USING btree (created_at);


--
-- TOC entry 5113 (class 1259 OID 17174)
-- Name: idx_invoice_customer_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_invoice_customer_code ON public.invoices USING btree (customer_code);


--
-- TOC entry 5114 (class 1259 OID 16904)
-- Name: idx_invoice_payment_status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_invoice_payment_status ON public.invoices USING btree (payment_status_id);


--
-- TOC entry 5115 (class 1259 OID 16905)
-- Name: idx_invoice_service_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_invoice_service_id ON public.invoices USING btree (service_id);


--
-- TOC entry 5109 (class 1259 OID 16906)
-- Name: idx_invoice_timeline_invoice_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_invoice_timeline_invoice_id ON public.invoice_timelines USING btree (invoice_id);


--
-- TOC entry 5123 (class 1259 OID 17202)
-- Name: idx_location_customer_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_location_customer_code ON public.locations USING btree (customer_code);


--
-- TOC entry 5124 (class 1259 OID 16908)
-- Name: idx_location_is_active; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_location_is_active ON public.locations USING btree (is_active);


--
-- TOC entry 5125 (class 1259 OID 16909)
-- Name: idx_location_is_primary; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_location_is_primary ON public.locations USING btree (is_primary);


--
-- TOC entry 5120 (class 1259 OID 16910)
-- Name: idx_location_timeline_location_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_location_timeline_location_id ON public.location_timelines USING btree (location_id);


--
-- TOC entry 5175 (class 1259 OID 17267)
-- Name: idx_payments_customer_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_payments_customer_code ON public.payments USING btree (customer_code);


--
-- TOC entry 5176 (class 1259 OID 17266)
-- Name: idx_payments_invoice_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_payments_invoice_id ON public.payments USING btree (invoice_id);


--
-- TOC entry 5177 (class 1259 OID 17268)
-- Name: idx_payments_received_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_payments_received_at ON public.payments USING btree (received_at);


--
-- TOC entry 5128 (class 1259 OID 16911)
-- Name: idx_reference_entry_category; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_reference_entry_category ON public.reference_entries USING btree (category);


--
-- TOC entry 5129 (class 1259 OID 16912)
-- Name: idx_reference_entry_is_active; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_reference_entry_is_active ON public.reference_entries USING btree (is_active);


--
-- TOC entry 5134 (class 1259 OID 16913)
-- Name: idx_report_module; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_report_module ON public.reports USING btree (module);


--
-- TOC entry 5141 (class 1259 OID 16914)
-- Name: idx_scheduler_event_is_active; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_scheduler_event_is_active ON public.scheduler_events USING btree (is_active);


--
-- TOC entry 5142 (class 1259 OID 16915)
-- Name: idx_scheduler_event_start_time; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_scheduler_event_start_time ON public.scheduler_events USING btree (start_time);


--
-- TOC entry 5143 (class 1259 OID 16916)
-- Name: idx_scheduler_event_status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_scheduler_event_status ON public.scheduler_events USING btree (status);


--
-- TOC entry 5146 (class 1259 OID 16917)
-- Name: idx_service_created_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_service_created_at ON public.services USING btree (created_at);


--
-- TOC entry 5147 (class 1259 OID 17167)
-- Name: idx_service_customer_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_service_customer_code ON public.services USING btree (customer_code);


--
-- TOC entry 5148 (class 1259 OID 16919)
-- Name: idx_service_is_active; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_service_is_active ON public.services USING btree (is_active);


--
-- TOC entry 5149 (class 1259 OID 18157)
-- Name: idx_services_due_date; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_services_due_date ON public.services USING btree (due_date);


--
-- TOC entry 5155 (class 1259 OID 16920)
-- Name: idx_ticket_assigned_to; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_ticket_assigned_to ON public.tickets USING btree (assigned_to);


--
-- TOC entry 5156 (class 1259 OID 16921)
-- Name: idx_ticket_created_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_ticket_created_at ON public.tickets USING btree (created_at);


--
-- TOC entry 5157 (class 1259 OID 17188)
-- Name: idx_ticket_customer_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_ticket_customer_code ON public.tickets USING btree (customer_code);


--
-- TOC entry 5158 (class 1259 OID 16923)
-- Name: idx_ticket_priority; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_ticket_priority ON public.tickets USING btree (priority);


--
-- TOC entry 5159 (class 1259 OID 16924)
-- Name: idx_ticket_status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_ticket_status ON public.tickets USING btree (status);


--
-- TOC entry 5152 (class 1259 OID 16925)
-- Name: idx_ticket_timeline_ticket_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_ticket_timeline_ticket_id ON public.ticket_timelines USING btree (ticket_id);


--
-- TOC entry 5160 (class 1259 OID 17290)
-- Name: idx_tickets_contact_mobile; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_tickets_contact_mobile ON public.tickets USING btree (contact_mobile);


--
-- TOC entry 5163 (class 1259 OID 17195)
-- Name: idx_trademark_customer_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_trademark_customer_code ON public.trademarks USING btree (customer_code);


--
-- TOC entry 5164 (class 1259 OID 16927)
-- Name: idx_trademark_is_active; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_trademark_is_active ON public.trademarks USING btree (is_active);


--
-- TOC entry 5167 (class 1259 OID 16928)
-- Name: idx_user_email; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_user_email ON public.users USING btree (email);


--
-- TOC entry 5168 (class 1259 OID 16929)
-- Name: idx_user_user_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_user_user_id ON public.users USING btree (user_id);


--
-- TOC entry 5180 (class 2606 OID 17155)
-- Name: customer_timelines customer_timelines_customer_code_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customer_timelines
    ADD CONSTRAINT customer_timelines_customer_code_fkey FOREIGN KEY (customer_code) REFERENCES public.customers(code) ON DELETE CASCADE;


--
-- TOC entry 5181 (class 2606 OID 16935)
-- Name: customers customers_business_type_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_business_type_id_fkey FOREIGN KEY (business_type_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5182 (class 2606 OID 16940)
-- Name: customers customers_city_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_city_id_fkey FOREIGN KEY (city_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5183 (class 2606 OID 16945)
-- Name: customers customers_country_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_country_id_fkey FOREIGN KEY (country_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5184 (class 2606 OID 16950)
-- Name: customers customers_industry_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_industry_id_fkey FOREIGN KEY (industry_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5185 (class 2606 OID 16955)
-- Name: customers customers_shop_size_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_shop_size_id_fkey FOREIGN KEY (shop_size_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5186 (class 2606 OID 16960)
-- Name: customers customers_state_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_state_id_fkey FOREIGN KEY (state_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5187 (class 2606 OID 16965)
-- Name: customers customers_tier_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_tier_id_fkey FOREIGN KEY (tier_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5188 (class 2606 OID 16970)
-- Name: customers customers_type_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_type_id_fkey FOREIGN KEY (type_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5189 (class 2606 OID 17280)
-- Name: customers fk_customers_customer_converted_by; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT fk_customers_customer_converted_by FOREIGN KEY (customer_converted_by) REFERENCES public.users(id);


--
-- TOC entry 5190 (class 2606 OID 17219)
-- Name: customers fk_customers_interaction_mode; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT fk_customers_interaction_mode FOREIGN KEY (interaction_mode_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5191 (class 2606 OID 17291)
-- Name: customers fk_customers_lead_source_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT fk_customers_lead_source_id FOREIGN KEY (lead_source_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5192 (class 2606 OID 17275)
-- Name: customers fk_customers_prospect_converted_by; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT fk_customers_prospect_converted_by FOREIGN KEY (prospect_converted_by) REFERENCES public.users(id);


--
-- TOC entry 5228 (class 2606 OID 17256)
-- Name: payments fk_payments_customer_code; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payments
    ADD CONSTRAINT fk_payments_customer_code FOREIGN KEY (customer_code) REFERENCES public.customers(code) ON DELETE CASCADE;


--
-- TOC entry 5229 (class 2606 OID 17251)
-- Name: payments fk_payments_invoice_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payments
    ADD CONSTRAINT fk_payments_invoice_id FOREIGN KEY (invoice_id) REFERENCES public.invoices(id) ON DELETE CASCADE;


--
-- TOC entry 5230 (class 2606 OID 17261)
-- Name: payments fk_payments_payment_mode_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payments
    ADD CONSTRAINT fk_payments_payment_mode_id FOREIGN KEY (payment_mode_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5193 (class 2606 OID 16975)
-- Name: implementation_assignments implementation_assignments_service_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.implementation_assignments
    ADD CONSTRAINT implementation_assignments_service_id_fkey FOREIGN KEY (service_id) REFERENCES public.services(id) ON DELETE CASCADE;


--
-- TOC entry 5194 (class 2606 OID 16980)
-- Name: implementation_timelines implementation_timelines_service_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.implementation_timelines
    ADD CONSTRAINT implementation_timelines_service_id_fkey FOREIGN KEY (service_id) REFERENCES public.services(id) ON DELETE CASCADE;


--
-- TOC entry 5195 (class 2606 OID 16985)
-- Name: implementation_timelines implementation_timelines_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.implementation_timelines
    ADD CONSTRAINT implementation_timelines_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id);


--
-- TOC entry 5196 (class 2606 OID 16990)
-- Name: investment_timelines investment_timelines_investment_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.investment_timelines
    ADD CONSTRAINT investment_timelines_investment_id_fkey FOREIGN KEY (investment_id) REFERENCES public.investments(id) ON DELETE CASCADE;


--
-- TOC entry 5197 (class 2606 OID 17176)
-- Name: investments investments_customer_code_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.investments
    ADD CONSTRAINT investments_customer_code_fkey FOREIGN KEY (customer_code) REFERENCES public.customers(code) ON DELETE CASCADE;


--
-- TOC entry 5198 (class 2606 OID 17000)
-- Name: investments investments_investment_type_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.investments
    ADD CONSTRAINT investments_investment_type_id_fkey FOREIGN KEY (investment_type_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5199 (class 2606 OID 17005)
-- Name: investments investments_staff_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.investments
    ADD CONSTRAINT investments_staff_id_fkey FOREIGN KEY (staff_id) REFERENCES public.users(id);


--
-- TOC entry 5200 (class 2606 OID 17010)
-- Name: invoice_timelines invoice_timelines_invoice_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoice_timelines
    ADD CONSTRAINT invoice_timelines_invoice_id_fkey FOREIGN KEY (invoice_id) REFERENCES public.invoices(id) ON DELETE CASCADE;


--
-- TOC entry 5201 (class 2606 OID 17169)
-- Name: invoices invoices_customer_code_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoices
    ADD CONSTRAINT invoices_customer_code_fkey FOREIGN KEY (customer_code) REFERENCES public.customers(code) ON DELETE CASCADE;


--
-- TOC entry 5202 (class 2606 OID 17020)
-- Name: invoices invoices_payment_mode_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoices
    ADD CONSTRAINT invoices_payment_mode_id_fkey FOREIGN KEY (payment_mode_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5203 (class 2606 OID 17025)
-- Name: invoices invoices_payment_status_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoices
    ADD CONSTRAINT invoices_payment_status_id_fkey FOREIGN KEY (payment_status_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5204 (class 2606 OID 17030)
-- Name: invoices invoices_service_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoices
    ADD CONSTRAINT invoices_service_id_fkey FOREIGN KEY (service_id) REFERENCES public.services(id) ON DELETE CASCADE;


--
-- TOC entry 5205 (class 2606 OID 17035)
-- Name: invoices invoices_staff_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invoices
    ADD CONSTRAINT invoices_staff_id_fkey FOREIGN KEY (staff_id) REFERENCES public.users(id);


--
-- TOC entry 5206 (class 2606 OID 17040)
-- Name: location_timelines location_timelines_location_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.location_timelines
    ADD CONSTRAINT location_timelines_location_id_fkey FOREIGN KEY (location_id) REFERENCES public.locations(id) ON DELETE CASCADE;


--
-- TOC entry 5207 (class 2606 OID 17045)
-- Name: locations locations_city_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.locations
    ADD CONSTRAINT locations_city_id_fkey FOREIGN KEY (city_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5208 (class 2606 OID 17050)
-- Name: locations locations_country_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.locations
    ADD CONSTRAINT locations_country_id_fkey FOREIGN KEY (country_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5209 (class 2606 OID 17197)
-- Name: locations locations_customer_code_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.locations
    ADD CONSTRAINT locations_customer_code_fkey FOREIGN KEY (customer_code) REFERENCES public.customers(code) ON DELETE CASCADE;


--
-- TOC entry 5210 (class 2606 OID 17060)
-- Name: locations locations_shop_size_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.locations
    ADD CONSTRAINT locations_shop_size_id_fkey FOREIGN KEY (shop_size_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5211 (class 2606 OID 17065)
-- Name: locations locations_state_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.locations
    ADD CONSTRAINT locations_state_id_fkey FOREIGN KEY (state_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5212 (class 2606 OID 17070)
-- Name: locations locations_tier_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.locations
    ADD CONSTRAINT locations_tier_id_fkey FOREIGN KEY (tier_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5213 (class 2606 OID 17162)
-- Name: services services_customer_code_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.services
    ADD CONSTRAINT services_customer_code_fkey FOREIGN KEY (customer_code) REFERENCES public.customers(code) ON DELETE CASCADE;


--
-- TOC entry 5214 (class 2606 OID 17080)
-- Name: services services_frequency_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.services
    ADD CONSTRAINT services_frequency_id_fkey FOREIGN KEY (frequency_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5215 (class 2606 OID 17085)
-- Name: services services_implementation_stage_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.services
    ADD CONSTRAINT services_implementation_stage_id_fkey FOREIGN KEY (implementation_stage_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5216 (class 2606 OID 17090)
-- Name: services services_project_manager_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.services
    ADD CONSTRAINT services_project_manager_id_fkey FOREIGN KEY (project_manager_id) REFERENCES public.users(id);


--
-- TOC entry 5217 (class 2606 OID 17095)
-- Name: services services_service_type_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.services
    ADD CONSTRAINT services_service_type_id_fkey FOREIGN KEY (service_type_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5218 (class 2606 OID 17100)
-- Name: services services_tax_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.services
    ADD CONSTRAINT services_tax_id_fkey FOREIGN KEY (tax_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5219 (class 2606 OID 17105)
-- Name: ticket_timelines ticket_timelines_ticket_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.ticket_timelines
    ADD CONSTRAINT ticket_timelines_ticket_id_fkey FOREIGN KEY (ticket_id) REFERENCES public.tickets(id) ON DELETE CASCADE;


--
-- TOC entry 5220 (class 2606 OID 17110)
-- Name: ticket_timelines ticket_timelines_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.ticket_timelines
    ADD CONSTRAINT ticket_timelines_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id);


--
-- TOC entry 5221 (class 2606 OID 17115)
-- Name: tickets tickets_assigned_to_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tickets
    ADD CONSTRAINT tickets_assigned_to_fkey FOREIGN KEY (assigned_to) REFERENCES public.users(id);


--
-- TOC entry 5222 (class 2606 OID 17183)
-- Name: tickets tickets_customer_code_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tickets
    ADD CONSTRAINT tickets_customer_code_fkey FOREIGN KEY (customer_code) REFERENCES public.customers(code) ON DELETE CASCADE;


--
-- TOC entry 5223 (class 2606 OID 17125)
-- Name: trademarks trademarks_city_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trademarks
    ADD CONSTRAINT trademarks_city_id_fkey FOREIGN KEY (city_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5224 (class 2606 OID 17130)
-- Name: trademarks trademarks_country_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trademarks
    ADD CONSTRAINT trademarks_country_id_fkey FOREIGN KEY (country_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5225 (class 2606 OID 17190)
-- Name: trademarks trademarks_customer_code_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trademarks
    ADD CONSTRAINT trademarks_customer_code_fkey FOREIGN KEY (customer_code) REFERENCES public.customers(code) ON DELETE CASCADE;


--
-- TOC entry 5226 (class 2606 OID 17140)
-- Name: trademarks trademarks_state_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trademarks
    ADD CONSTRAINT trademarks_state_id_fkey FOREIGN KEY (state_id) REFERENCES public.reference_entries(id);


--
-- TOC entry 5227 (class 2606 OID 17145)
-- Name: trademarks trademarks_tier_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trademarks
    ADD CONSTRAINT trademarks_tier_id_fkey FOREIGN KEY (tier_id) REFERENCES public.reference_entries(id);


-- Completed on 2026-04-01 18:32:45

--
-- PostgreSQL database dump complete
--
--
-- PostgreSQL database dump
--


-- Dumped from database version 18.3
-- Dumped by pg_dump version 18.3

-- Started on 2026-04-01 18:36:20

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 5299 (class 0 OID 16644)
-- Dependencies: 241
-- Data for Name: reference_entries; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.reference_entries (id, category, label, value, is_active, sort_order, requires_implementation, is_implementation) FROM stdin;
1	Business Type	Startup	startup	t	1	\N	\N
2	Business Type	SME	sme	t	2	\N	\N
3	Business Type	Enterprise	enterprise	t	3	\N	\N
4	Industry	Technology	technology	t	1	\N	\N
5	Industry	Finance	finance	t	2	\N	\N
6	Industry	Healthcare	healthcare	t	3	\N	\N
7	Industry	Manufacturing	manufacturing	t	4	\N	\N
8	City	Mumbai	mumbai	t	1	\N	\N
9	City	Bangalore	bangalore	t	2	\N	\N
10	City	Delhi	delhi	t	3	\N	\N
11	State	Maharashtra	maharashtra	t	1	\N	\N
12	State	Karnataka	karnataka	t	2	\N	\N
13	State	Delhi NCR	delhi_ncr	t	3	\N	\N
14	Country	India	india	t	1	\N	\N
15	Country	USA	usa	t	2	\N	\N
17	Service Type	ERP	erp	t	2	t	\N
18	Service Type	AMC	amc	t	3	f	\N
19	Service Type	Implementation	implementation	t	4	t	\N
20	Service	ERP License	ERP_LICENSE	t	1	\N	f
21	Service	AMC	AMC	t	2	\N	f
22	Service	Customization	CUSTOMIZE	t	3	\N	t
23	Service	Training	TRAINING	t	4	\N	t
24	Service	Hosting Charges	HOSTING	t	5	\N	f
25	Service	Subscription	SAAS	t	6	\N	f
26	Service	Feature Enabling	FEATURES	t	7	\N	t
27	Service	Implementation	IMPLEMENTATION	t	8	\N	t
28	Service	E-Commerce	E-COMMERCE	t	9	\N	t
29	Shop Size	Micro Store (0-2000)	0-2000	t	1	\N	\N
30	Shop Size	Small Retail (2000-5000)	2000-5000	t	2	\N	\N
31	Shop Size	Medium Retail (5000-10000)	5000-10000	t	3	\N	\N
32	Shop Size	Large Retail (10000-30000)	10000-30000	t	4	\N	\N
33	Shop Size	Mega Store (30000-100000)	30000-100000	t	5	\N	\N
34	Shop Size	Hypermart Store (100000+)	100000+	t	6	\N	\N
35	City Tier	Tier I	TIER_I	t	1	\N	\N
36	City Tier	Tier II	TIER_II	t	2	\N	\N
37	City Tier	Tier III	TIER_III	t	3	\N	\N
38	Service Status	Active	active	t	1	\N	\N
39	Service Status	On Hold	on_hold	t	2	\N	\N
40	Service Status	Completed	completed	t	3	\N	\N
41	Tax	GST 18%	gst_18	t	1	\N	\N
42	Tax	GST 12%	gst_12	t	2	\N	\N
43	Tax	No Tax	no_tax	t	3	\N	\N
44	Frequency	Monthly	monthly	t	1	\N	\N
45	Frequency	Yearly	yearly	t	2	\N	\N
46	Frequency	One-Time	one_time	t	3	\N	\N
47	Payment Frequency	Yearly	YEARLY	t	1	\N	\N
48	Payment Frequency	Monthly	MONTHLY	t	2	\N	\N
49	Payment Frequency	One Time	ONE_TIME	t	3	\N	\N
50	Inventory Value Unit	Lakhs	LAKH	t	1	\N	\N
51	Inventory Value Unit	Crores	CRORE	t	2	\N	\N
52	Payment Mode	Bank Transfer	bank_transfer	t	1	\N	\N
53	Payment Mode	UPI	upi	t	2	\N	\N
54	Payment Mode	Cash	cash	t	3	\N	\N
55	Payment Mode	Cheque	cheque	t	4	\N	\N
56	Payment Mode	Online Account Transfer	ONLINE_ACCOUNT	t	1	\N	\N
57	Payment Mode	UPI (BHIM / Gpay)	UPI	t	2	\N	\N
58	Payment Mode	Cheque	CHEQUE	t	3	\N	\N
59	Payment Status	Paid	paid	t	1	\N	\N
60	Payment Status	Pending	pending	t	2	\N	\N
61	Payment Status	Overdue	overdue	t	3	\N	\N
62	Payment Status	Failed	failed	t	4	\N	\N
63	Investment Type	Equity	equity	t	1	\N	\N
64	Investment Type	Debt	debt	t	2	\N	\N
65	Investment Type	Convertible Note	convertible_note	t	3	\N	\N
66	Industry	Apparel	apparel	t	5	\N	\N
67	Industry	Mobile	mobile	t	6	\N	\N
68	Industry	Footwear	footwear	t	7	\N	\N
69	Industry	Cosmetics	cosmetics	t	8	\N	\N
70	Business Nature	Retail Shops	RETAIL	t	1	\N	\N
71	Business Nature	Manufacturers	MANUFACTURER	t	2	\N	\N
72	Business Nature	Large Format	LARGE_FORMAT	t	3	\N	\N
74	Implementation Status	In Progress	in_progress	t	1	\N	\N
75	Implementation Status	Completed	completed	t	2	\N	\N
76	Implementation Stage	Discovery	discovery	t	1	\N	\N
77	Implementation Stage	Planning	planning	t	2	\N	\N
78	Implementation Stage	Execution	execution	t	3	\N	\N
79	Implementation Stage	Review	review	t	4	\N	\N
80	Implementation Stage	Handover	handover	t	5	\N	\N
81	Ticket Category	Bug	bug	t	1	\N	\N
82	Ticket Category	Feature Request	feature_request	t	2	\N	\N
83	Ticket Category	Performance	performance	t	3	\N	\N
84	Ticket Category	Billing	billing	t	4	\N	\N
85	Lead Source	Website	website	t	1	\N	\N
86	Lead Source	Referral	referral	t	2	\N	\N
87	Lead Source	Webinar	webinar	t	3	\N	\N
88	Customer Type	Lead	lead	t	1	\N	\N
89	Customer Type	Prospect	prospect	t	2	\N	\N
90	Customer Type	Customer	customer	t	3	\N	\N
92	Implementation Status	Open	open	t	1	\N	\N
93	Customer Type	Loss	loss	t	4	\N	\N
94	State	Tamil Nadu	tamil_nadu	t	4	\N	\N
95	City	Cuddalore	cuddalore	t	4	\N	\N
96	Implementation Status	start	start	t	0	\N	\N
97	City	Chennai	chennai	t	5	\N	\N
98	Interaction Mode	Call	call	t	1	\N	\N
99	Interaction Mode	Visit	visit	t	2	\N	\N
100	Interaction Mode	Demo	demo	t	3	\N	\N
101	Lead Source	External	external	t	1	\N	\N
102	Lead Source	Internal	internal	t	2	\N	\N
103	Lead Source	Exhibition / Fair	exhibition_fair	t	3	\N	\N
104	Lead Source	Social Media Campaign	social_media_campaign	t	4	\N	\N
105	City	Kolar	kolar	t	6	\N	\N
16	Service Type	SaaS	saas	t	1	f	\N
\.


--
-- TOC entry 5315 (class 0 OID 16800)
-- Dependencies: 257
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.users (id, user_id, first_name, last_name, email, role, is_active, last_login, created_at, created_by, modified_at, modified_by, password_hash) FROM stdin;
11	admin	Admin	User	admin@crm	Admin	t	2026-04-01 16:01:20.051445	2026-03-31 18:07:53.045424	\N	2026-04-01 16:01:20.051445	\N	$2y$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi
12	aravindan	aravindan	m	aravindan@gmail.com	Sales Executive	t	2026-04-01 18:30:42.074453	2026-04-01 15:51:25.579551	1	2026-04-01 18:30:42.074515	1	$2a$11$8/5SgyGU8jnr6hJT3DNqXO0eHWeL3j88TO4UuzsAt1DVNAJ5ssREy
\.


--
-- TOC entry 5279 (class 0 OID 16456)
-- Dependencies: 221
-- Data for Name: customers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.customers (id, code, reg_name, mobile, email, business_type_id, industry_id, address_line1, address_line2, city_id, state_id, country_id, pincode, gst_number, contact_persons, emails, mobiles, shop_size_id, tier_id, type_id, is_active, total_locations, total_trade_names, created_at, created_by, converted_at, converted_by, modified_at, modified_by, pipeline_status, product_features_discussed, assigned_representative_id, interaction_mode_id, price_plan_selected, quotation_prepared_sent, quotation_accepted, advance_payment_received, invoice_generated, invoice_number, prospect_converted_at, prospect_converted_by, customer_converted_at, customer_converted_by, lead_source_id) FROM stdin;
\.


--
-- TOC entry 5277 (class 0 OID 16439)
-- Dependencies: 219
-- Data for Name: customer_timelines; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.customer_timelines (id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by, customer_code) FROM stdin;
\.


--
-- TOC entry 5281 (class 0 OID 16477)
-- Dependencies: 223
-- Data for Name: files; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.files (id, is_factory, content, version, created_by, created_on, modified_by, modified_on, attributes, is_active, is_suspended, parent_id, notes, type) FROM stdin;
\.


--
-- TOC entry 5307 (class 0 OID 16712)
-- Dependencies: 249
-- Data for Name: services; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.services (id, location_id, trade_name_id, service_type_id, frequency_id, live_date, service_value, due_month, implementation_required, implementation_stage_id, implementation_started_at, implementation_started_by, implementation_completed_at, implementation_completed_by, project_title, project_manager_id, budget_amount, progress_percentage, tax_id, notes, is_active, created_at, created_by, modified_at, modified_by, implementation_status, customer_code, due_date, amc_percentage, amc_amount) FROM stdin;
\.


--
-- TOC entry 5283 (class 0 OID 16495)
-- Dependencies: 225
-- Data for Name: implementation_assignments; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.implementation_assignments (id, service_id, user_ids) FROM stdin;
\.


--
-- TOC entry 5285 (class 0 OID 16504)
-- Dependencies: 227
-- Data for Name: implementation_timelines; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.implementation_timelines (id, service_id, type, status, notes, file_id, file_name, user_id, is_active, created_at, created_by, modified_at, modified_by) FROM stdin;
\.


--
-- TOC entry 5289 (class 0 OID 16540)
-- Dependencies: 231
-- Data for Name: investments; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.investments (id, location_id, amount, investment_type_id, staff_id, notes, is_active, created_at, created_by, modified_at, modified_by, customer_code, claimed_amount, remaining_amount, claimed_fully, claimed_at, claimed_by, claim_notes, needs_claim) FROM stdin;
\.


--
-- TOC entry 5287 (class 0 OID 16523)
-- Dependencies: 229
-- Data for Name: investment_timelines; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.investment_timelines (id, investment_id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by) FROM stdin;
\.


--
-- TOC entry 5293 (class 0 OID 16575)
-- Dependencies: 235
-- Data for Name: invoices; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.invoices (id, invoice_number, service_id, staff_id, payment_mode_id, payment_status_id, receivable, received, subscription_start_at, subscription_end_at, is_active, created_at, created_by, paid_at, paid_by, modified_at, modified_by, customer_code) FROM stdin;
\.


--
-- TOC entry 5291 (class 0 OID 16558)
-- Dependencies: 233
-- Data for Name: invoice_timelines; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.invoice_timelines (id, invoice_id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by) FROM stdin;
\.


--
-- TOC entry 5297 (class 0 OID 16613)
-- Dependencies: 239
-- Data for Name: locations; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.locations (id, code, name, reg_name, pincode, city_id, state_id, country_id, address_line1, address_line2, contact_persons, emails, mobiles, shop_size_id, tier_id, is_primary, gst_number, is_active, created_at, created_by, modified_at, modified_by, customer_code) FROM stdin;
\.


--
-- TOC entry 5295 (class 0 OID 16596)
-- Dependencies: 237
-- Data for Name: location_timelines; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.location_timelines (id, location_id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by) FROM stdin;
\.


--
-- TOC entry 5318 (class 0 OID 17227)
-- Dependencies: 260
-- Data for Name: payments; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.payments (id, invoice_id, customer_code, amount, remaining, payment_mode_id, received_at, notes, is_active, created_at, created_by, modified_at, modified_by) FROM stdin;
\.


--
-- TOC entry 5301 (class 0 OID 16656)
-- Dependencies: 243
-- Data for Name: reports; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.reports (id, name, module, columns, filters, group_by, sort_by, is_active, created_by, created_at, last_run, modified_at, modified_by, query) FROM stdin;
\.


--
-- TOC entry 5303 (class 0 OID 16675)
-- Dependencies: 245
-- Data for Name: roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.roles (id, name, description, permissions, user_count, created_at, created_by, modified_at, modified_by) FROM stdin;
16	Implementation Engineer	Implementation engineer access	contacts.own,reports.view,users.view	1	2026-04-01 16:03:51.309909	1	2026-04-01 16:10:35.070395	1
13	Business Development Executive	Business development executive access	all,contacts.all,contacts.own,contacts.view,pipeline.all,pipeline.own,pipeline.view,tickets.all,tickets.own,tickets.view,reports.all,reports.view,reports.edit,payments.all,payments.view,payments.edit,users.all,users.view,users.edit,roles.all,roles.view,roles.edit,settings.all,settings.view	1	2026-04-01 16:03:51.309909	1	2026-04-01 16:13:54.20445	1
11	Super Admin	Full system access	all,contacts.all,contacts.own,contacts.view,pipeline.all,pipeline.own,pipeline.view,tickets.all,tickets.own,tickets.view,reports.all,reports.view,reports.edit,payments.all,payments.view,payments.edit,users.all,users.view,users.edit,roles.all,roles.view,roles.edit,settings.all,settings.view	1	2026-04-01 16:03:51.309909	1	2026-04-01 16:14:00.9775	1
15	Senior Support Specialist	Senior support specialist access	all,contacts.all,contacts.own,contacts.view,pipeline.all,pipeline.own,pipeline.view,tickets.all,tickets.own,tickets.view,reports.all,reports.view,reports.edit,payments.all,payments.view,payments.edit,users.all,users.view,users.edit,roles.all,roles.view,roles.edit,settings.all,settings.view	1	2026-04-01 16:03:51.309909	1	2026-04-01 16:14:12.020959	1
17	Senior Implementation Engineer	Senior implementation engineer access	all,contacts.all,contacts.own,contacts.view,pipeline.all,pipeline.own,pipeline.view,tickets.all,tickets.own,tickets.view,reports.all,reports.view,reports.edit,payments.all,payments.view,payments.edit,users.all,users.view,users.edit,roles.all,roles.view,roles.edit,settings.all,settings.view	1	2026-04-01 16:03:51.309909	1	2026-04-01 16:14:18.416852	1
14	Support Specialist	Support specialist access	all,contacts.all,contacts.own,contacts.view,pipeline.all,pipeline.own,pipeline.view,tickets.all,tickets.own,tickets.view,reports.all,reports.view,reports.edit,payments.all,payments.view,payments.edit,users.all,users.view,users.edit,roles.all,roles.view,roles.edit,settings.all,settings.view	1	2026-04-01 16:03:51.309909	1	2026-04-01 16:14:26.490509	1
12	Sales Executive	Sales executive access	contacts.all,contacts.own,contacts.view,pipeline.all,pipeline.own,pipeline.view,reports.all,reports.view,reports.edit,payments.all,payments.view,payments.edit	1	2026-04-01 16:03:51.309909	1	2026-04-01 16:15:01.164276	1
\.


--
-- TOC entry 5305 (class 0 OID 16689)
-- Dependencies: 247
-- Data for Name: scheduler_events; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.scheduler_events (id, title, description, start_time, end_time, attendees, location, type, priority, is_active, related_to_type, related_to_id, created_by, created_at, modified_at, modified_by, status) FROM stdin;
\.


--
-- TOC entry 5311 (class 0 OID 16751)
-- Dependencies: 253
-- Data for Name: tickets; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.tickets (id, location_id, subject, description, status, priority, assigned_to, sla_deadline, is_active, created_at, created_by, closed_at, closed_by, modified_at, modified_by, category, module, customer_code, contact_person, contact_mobile) FROM stdin;
\.


--
-- TOC entry 5309 (class 0 OID 16733)
-- Dependencies: 251
-- Data for Name: ticket_timelines; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.ticket_timelines (id, ticket_id, type, notes, file_id, file_name, user_id, is_active, created_at, created_by, modified_at, modified_by) FROM stdin;
\.


--
-- TOC entry 5313 (class 0 OID 16775)
-- Dependencies: 255
-- Data for Name: trademarks; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.trademarks (id, location_id, reg_name, gst_number, pincode, city_id, state_id, country_id, address_line1, address_line2, contact_persons, emails, mobiles, tier_id, shop_size_id, registration_number, category, description, registration_date, expiry_date, is_active, remarks, created_at, created_by, modified_at, modified_by, customer_code) FROM stdin;
\.


--
-- TOC entry 5324 (class 0 OID 0)
-- Dependencies: 220
-- Name: customer_timelines_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.customer_timelines_id_seq', 1598, true);


--
-- TOC entry 5325 (class 0 OID 0)
-- Dependencies: 222
-- Name: customers_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.customers_id_seq', 1567, true);


--
-- TOC entry 5326 (class 0 OID 0)
-- Dependencies: 224
-- Name: files_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.files_id_seq', 1, false);


--
-- TOC entry 5327 (class 0 OID 0)
-- Dependencies: 226
-- Name: implementation_assignments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.implementation_assignments_id_seq', 9, true);


--
-- TOC entry 5328 (class 0 OID 0)
-- Dependencies: 228
-- Name: implementation_timelines_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.implementation_timelines_id_seq', 5, true);


--
-- TOC entry 5329 (class 0 OID 0)
-- Dependencies: 230
-- Name: investment_timelines_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.investment_timelines_id_seq', 3, true);


--
-- TOC entry 5330 (class 0 OID 0)
-- Dependencies: 232
-- Name: investments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.investments_id_seq', 9, true);


--
-- TOC entry 5331 (class 0 OID 0)
-- Dependencies: 234
-- Name: invoice_timelines_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.invoice_timelines_id_seq', 26, true);


--
-- TOC entry 5332 (class 0 OID 0)
-- Dependencies: 236
-- Name: invoices_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.invoices_id_seq', 50, true);


--
-- TOC entry 5333 (class 0 OID 0)
-- Dependencies: 238
-- Name: location_timelines_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.location_timelines_id_seq', 1, false);


--
-- TOC entry 5334 (class 0 OID 0)
-- Dependencies: 240
-- Name: locations_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.locations_id_seq', 10, true);


--
-- TOC entry 5335 (class 0 OID 0)
-- Dependencies: 259
-- Name: payments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.payments_id_seq', 8, true);


--
-- TOC entry 5336 (class 0 OID 0)
-- Dependencies: 242
-- Name: reference_entries_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.reference_entries_id_seq', 105, true);


--
-- TOC entry 5337 (class 0 OID 0)
-- Dependencies: 244
-- Name: reports_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.reports_id_seq', 7, true);


--
-- TOC entry 5338 (class 0 OID 0)
-- Dependencies: 246
-- Name: roles_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.roles_id_seq', 17, true);


--
-- TOC entry 5339 (class 0 OID 0)
-- Dependencies: 248
-- Name: scheduler_events_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.scheduler_events_id_seq', 2, true);


--
-- TOC entry 5340 (class 0 OID 0)
-- Dependencies: 250
-- Name: services_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.services_id_seq', 39, true);


--
-- TOC entry 5341 (class 0 OID 0)
-- Dependencies: 252
-- Name: ticket_timelines_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.ticket_timelines_id_seq', 23, true);


--
-- TOC entry 5342 (class 0 OID 0)
-- Dependencies: 254
-- Name: tickets_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.tickets_id_seq', 10, true);


--
-- TOC entry 5343 (class 0 OID 0)
-- Dependencies: 256
-- Name: trademarks_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.trademarks_id_seq', 9, true);


--
-- TOC entry 5344 (class 0 OID 0)
-- Dependencies: 258
-- Name: users_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.users_id_seq', 12, true);


-- Completed on 2026-04-01 18:36:20

--
-- PostgreSQL database dump complete
--




