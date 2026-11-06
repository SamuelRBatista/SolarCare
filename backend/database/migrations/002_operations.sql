CREATE TABLE customers (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    name varchar(160) NOT NULL,
    kind varchar(16) NOT NULL,
    tax_id_ciphertext text,
    tax_id_lookup bytea,
    phone varchar(40),
    email varchar(254),
    address varchar(240),
    city varchar(120),
    state char(2),
    notes varchar(4000),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id),
    UNIQUE (tenant_id, id),
    CHECK (length(trim(name)) > 0),
    CHECK (kind IN ('Person', 'Company')),
    CHECK ((tax_id_ciphertext IS NULL) = (tax_id_lookup IS NULL)),
    CHECK (tax_id_lookup IS NULL OR octet_length(tax_id_lookup) = 32),
    CHECK (email IS NULL OR email = lower(email)),
    CHECK (state IS NULL OR state ~ '^[A-Z]{2}$')
);

CREATE UNIQUE INDEX ux_customers_tenant_tax_id
    ON customers (tenant_id, tax_id_lookup)
    WHERE tax_id_lookup IS NOT NULL;
CREATE INDEX ix_customers_tenant_name ON customers (tenant_id, lower(name));
CREATE INDEX ix_customers_tenant_active ON customers (tenant_id, is_active);

CREATE TABLE installations (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    customer_id uuid NOT NULL,
    name varchar(160) NOT NULL,
    address varchar(240),
    city varchar(120),
    state char(2),
    installed_at date,
    capacity_kwp numeric(10, 3),
    module_count integer,
    module_manufacturer varchar(120),
    module_model varchar(120),
    inverter_manufacturer varchar(120),
    inverter_model varchar(120),
    integrator_name varchar(160),
    technical_responsible varchar(160),
    notes varchar(4000),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, customer_id)
        REFERENCES customers (tenant_id, id) ON DELETE RESTRICT,
    CHECK (length(trim(name)) > 0),
    CHECK (capacity_kwp IS NULL OR capacity_kwp > 0),
    CHECK (module_count IS NULL OR module_count > 0),
    CHECK (state IS NULL OR state ~ '^[A-Z]{2}$')
);

CREATE INDEX ix_installations_tenant_customer
    ON installations (tenant_id, customer_id, is_active);

CREATE TABLE equipment (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    installation_id uuid NOT NULL,
    category varchar(40) NOT NULL,
    manufacturer varchar(120) NOT NULL,
    model varchar(120),
    serial_number varchar(160),
    acquired_at date,
    installed_at date,
    warranty_expires_at date,
    status varchar(24) NOT NULL DEFAULT 'Active',
    notes varchar(4000),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, installation_id)
        REFERENCES installations (tenant_id, id) ON DELETE RESTRICT,
    CHECK (length(trim(category)) > 0),
    CHECK (length(trim(manufacturer)) > 0),
    CHECK (status IN ('Active', 'UnderMaintenance', 'Retired'))
);

CREATE INDEX ix_equipment_tenant_installation
    ON equipment (tenant_id, installation_id, is_active);
CREATE INDEX ix_equipment_tenant_warranty
    ON equipment (tenant_id, warranty_expires_at)
    WHERE is_active = true AND warranty_expires_at IS NOT NULL;

CREATE TABLE service_tickets (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    protocol varchar(32) NOT NULL,
    customer_id uuid NOT NULL,
    installation_id uuid,
    equipment_id uuid,
    description varchar(8000) NOT NULL,
    category varchar(80) NOT NULL,
    priority varchar(16) NOT NULL DEFAULT 'Normal',
    status varchar(24) NOT NULL DEFAULT 'Open',
    assigned_user_id uuid,
    opened_by_user_id uuid NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    opened_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    closed_at timestamptz,
    PRIMARY KEY (id),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, protocol),
    FOREIGN KEY (tenant_id, customer_id)
        REFERENCES customers (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, installation_id)
        REFERENCES installations (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, equipment_id)
        REFERENCES equipment (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, assigned_user_id)
        REFERENCES users (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, opened_by_user_id)
        REFERENCES users (tenant_id, id) ON DELETE RESTRICT,
    CHECK (length(trim(description)) > 0),
    CHECK (length(trim(category)) > 0),
    CHECK (priority IN ('Low', 'Normal', 'High', 'Urgent')),
    CHECK (status IN ('Open', 'InProgress', 'WaitingCustomer', 'WaitingParts', 'Resolved', 'Closed', 'Cancelled')),
    CHECK (closed_at IS NULL OR status IN ('Closed', 'Cancelled'))
);

CREATE INDEX ix_service_tickets_tenant_status
    ON service_tickets (tenant_id, status, opened_at DESC);
CREATE INDEX ix_service_tickets_tenant_customer
    ON service_tickets (tenant_id, customer_id, opened_at DESC);

CREATE TABLE service_ticket_history (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    ticket_id uuid NOT NULL,
    actor_user_id uuid NOT NULL,
    action varchar(40) NOT NULL,
    old_value varchar(2000),
    new_value varchar(2000),
    note varchar(2000),
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (tenant_id, ticket_id)
        REFERENCES service_tickets (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, actor_user_id)
        REFERENCES users (tenant_id, id) ON DELETE RESTRICT
);

CREATE INDEX ix_service_ticket_history_tenant_ticket
    ON service_ticket_history (tenant_id, ticket_id, created_at);

CREATE TABLE service_orders (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    ticket_id uuid,
    installation_id uuid NOT NULL,
    technician_user_id uuid,
    scheduled_at timestamptz,
    address varchar(240),
    description varchar(8000) NOT NULL,
    status varchar(24) NOT NULL DEFAULT 'Pending',
    checklist jsonb NOT NULL DEFAULT '[]'::jsonb,
    materials jsonb NOT NULL DEFAULT '[]'::jsonb,
    hours_worked numeric(7, 2),
    technical_report varchar(8000),
    completed_at timestamptz,
    created_by_user_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, ticket_id)
        REFERENCES service_tickets (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, installation_id)
        REFERENCES installations (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, technician_user_id)
        REFERENCES users (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, created_by_user_id)
        REFERENCES users (tenant_id, id) ON DELETE RESTRICT,
    CHECK (length(trim(description)) > 0),
    CHECK (status IN ('Pending', 'Scheduled', 'InProgress', 'Completed', 'Cancelled')),
    CHECK (hours_worked IS NULL OR hours_worked >= 0),
    CHECK (completed_at IS NULL OR status = 'Completed'),
    CHECK (jsonb_typeof(checklist) = 'array'),
    CHECK (jsonb_typeof(materials) = 'array')
);

CREATE INDEX ix_service_orders_tenant_status_schedule
    ON service_orders (tenant_id, status, scheduled_at);

CREATE TABLE service_order_history (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    service_order_id uuid NOT NULL,
    actor_user_id uuid NOT NULL,
    action varchar(40) NOT NULL,
    old_status varchar(24),
    new_status varchar(24),
    note varchar(2000),
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (tenant_id, service_order_id)
        REFERENCES service_orders (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, actor_user_id)
        REFERENCES users (tenant_id, id) ON DELETE RESTRICT
);

CREATE INDEX ix_service_order_history_tenant_order
    ON service_order_history (tenant_id, service_order_id, created_at);

CREATE TABLE maintenance_plans (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    installation_id uuid NOT NULL,
    name varchar(160) NOT NULL,
    interval_days integer NOT NULL,
    next_due_at date NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    notes varchar(4000),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, installation_id)
        REFERENCES installations (tenant_id, id) ON DELETE RESTRICT,
    CHECK (length(trim(name)) > 0),
    CHECK (interval_days BETWEEN 1 AND 3650)
);

CREATE INDEX ix_maintenance_plans_tenant_due
    ON maintenance_plans (tenant_id, next_due_at)
    WHERE is_active = true;

CREATE TABLE maintenance_records (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES tenants (id) ON DELETE RESTRICT,
    maintenance_plan_id uuid NOT NULL,
    installation_id uuid NOT NULL,
    service_order_id uuid,
    performed_by_user_id uuid NOT NULL,
    performed_at timestamptz NOT NULL,
    notes varchar(4000),
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id),
    FOREIGN KEY (tenant_id, maintenance_plan_id)
        REFERENCES maintenance_plans (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, installation_id)
        REFERENCES installations (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, service_order_id)
        REFERENCES service_orders (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, performed_by_user_id)
        REFERENCES users (tenant_id, id) ON DELETE RESTRICT
);

CREATE INDEX ix_maintenance_records_tenant_plan
    ON maintenance_records (tenant_id, maintenance_plan_id, performed_at DESC);
