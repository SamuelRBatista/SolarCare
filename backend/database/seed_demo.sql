BEGIN;

DO $$
DECLARE
    demo_tenant_id uuid;
    demo_user_id uuid;
BEGIN
    SELECT t.id, u.id
    INTO demo_tenant_id, demo_user_id
    FROM tenants t
    JOIN users u ON u.tenant_id = t.id
    WHERE t.is_active = TRUE AND u.is_active = TRUE
    ORDER BY t.created_at, (u.role = 'CompanyAdmin') DESC, u.created_at
    LIMIT 1;

    IF demo_tenant_id IS NULL OR demo_user_id IS NULL THEN
        RAISE EXCEPTION 'No active tenant and user were found for demo data.';
    END IF;

    INSERT INTO customers (
        id, tenant_id, name, kind, phone, email, address, city, state, notes
    )
    VALUES
        ('00000000-0000-4000-8000-000000000101', demo_tenant_id, 'Brisa Solar (Demo)', 'Company',
         '(11) 5555-0101', 'brisa.demo@example.invalid', 'Rua das Palmeiras, 100',
         'Campinas', 'SP', 'Registro fictício para demonstração.'),
        ('00000000-0000-4000-8000-000000000102', demo_tenant_id, 'Casa Horizonte (Demo)', 'Person',
         '(21) 5555-0102', 'horizonte.demo@example.invalid', 'Av. do Sol, 250',
         'Niterói', 'RJ', 'Registro fictício para demonstração.'),
        ('00000000-0000-4000-8000-000000000103', demo_tenant_id, 'Mercado Verde (Demo)', 'Company',
         '(31) 5555-0103', 'mercado.demo@example.invalid', 'Praça Central, 45',
         'Belo Horizonte', 'MG', 'Registro fictício para demonstração.')
    ON CONFLICT DO NOTHING;

    INSERT INTO installations (
        id, tenant_id, customer_id, name, address, city, state, installed_at,
        capacity_kwp, module_count, module_manufacturer, module_model,
        inverter_manufacturer, inverter_model, integrator_name, technical_responsible, notes
    )
    VALUES
        ('00000000-0000-4000-8000-000000000201', demo_tenant_id,
         '00000000-0000-4000-8000-000000000101', 'Usina Comercial Brisa (Demo)',
         'Rua das Palmeiras, 100', 'Campinas', 'SP', CURRENT_DATE - 400,
         48.000, 80, 'Solaris Demo', 'SD-600', 'Sol Inverter', 'SI-50K',
         'Equipe SolarCare Demo', 'Técnico Demonstração', 'Instalação fictícia para testes.'),
        ('00000000-0000-4000-8000-000000000202', demo_tenant_id,
         '00000000-0000-4000-8000-000000000102', 'Residencial Horizonte (Demo)',
         'Av. do Sol, 250', 'Niterói', 'RJ', CURRENT_DATE - 120,
         8.250, 15, 'Solaris Demo', 'SD-550', 'Volt Inverter', 'VI-8K',
         'Equipe SolarCare Demo', 'Técnico Demonstração', 'Instalação fictícia para testes.'),
        ('00000000-0000-4000-8000-000000000203', demo_tenant_id,
         '00000000-0000-4000-8000-000000000103', 'Loja Mercado Verde (Demo)',
         'Praça Central, 45', 'Belo Horizonte', 'MG', CURRENT_DATE - 250,
         21.000, 35, 'Solaris Demo', 'SD-600', 'Sol Inverter', 'SI-25K',
         'Equipe SolarCare Demo', 'Técnico Demonstração', 'Instalação fictícia para testes.')
    ON CONFLICT DO NOTHING;

    INSERT INTO equipment (
        id, tenant_id, installation_id, category, manufacturer, model, serial_number,
        acquired_at, installed_at, warranty_expires_at, status, notes
    )
    VALUES
        ('00000000-0000-4000-8000-000000000301', demo_tenant_id,
         '00000000-0000-4000-8000-000000000201', 'Inverter', 'Sol Inverter', 'SI-50K',
         'DEMO-SI-50001', CURRENT_DATE - 400, CURRENT_DATE - 400, CURRENT_DATE + 500,
         'Active', 'Equipamento fictício para demonstração.'),
        ('00000000-0000-4000-8000-000000000302', demo_tenant_id,
         '00000000-0000-4000-8000-000000000202', 'Inverter', 'Volt Inverter', 'VI-8K',
         'DEMO-VI-80002', CURRENT_DATE - 120, CURRENT_DATE - 120, CURRENT_DATE + 35,
         'Active', 'Garantia próxima do vencimento; dado fictício.'),
        ('00000000-0000-4000-8000-000000000303', demo_tenant_id,
         '00000000-0000-4000-8000-000000000203', 'Inverter', 'Sol Inverter', 'SI-25K',
         'DEMO-SI-25003', CURRENT_DATE - 250, CURRENT_DATE - 250, CURRENT_DATE + 400,
         'UnderMaintenance', 'Equipamento fictício em manutenção.')
    ON CONFLICT DO NOTHING;

    INSERT INTO service_tickets (
        id, tenant_id, protocol, customer_id, installation_id, equipment_id,
        description, category, priority, status, assigned_user_id, opened_by_user_id,
        opened_at, updated_at
    )
    VALUES
        ('00000000-0000-4000-8000-000000000401', demo_tenant_id, 'DEMO-2026-001',
         '00000000-0000-4000-8000-000000000101',
         '00000000-0000-4000-8000-000000000201',
         '00000000-0000-4000-8000-000000000301',
         'Monitoramento sem comunicação desde ontem. Verificar conexão e inversor.',
         'Inverter', 'High', 'Open', demo_user_id, demo_user_id,
         now() - interval '1 day', now() - interval '1 day'),
        ('00000000-0000-4000-8000-000000000402', demo_tenant_id, 'DEMO-2026-002',
         '00000000-0000-4000-8000-000000000102',
         '00000000-0000-4000-8000-000000000202',
         '00000000-0000-4000-8000-000000000302',
         'Cliente relata geração abaixo do esperado. Analisar telemetria do sistema.',
         'Generation', 'Normal', 'InProgress', demo_user_id, demo_user_id,
         now() - interval '3 days', now() - interval '2 days'),
        ('00000000-0000-4000-8000-000000000403', demo_tenant_id, 'DEMO-2026-003',
         '00000000-0000-4000-8000-000000000103',
         '00000000-0000-4000-8000-000000000203',
         '00000000-0000-4000-8000-000000000303',
         'Inspecionar inversor e substituir componente após chegada da peça.',
         'Maintenance', 'Urgent', 'WaitingParts', demo_user_id, demo_user_id,
         now() - interval '4 days', now() - interval '1 day')
    ON CONFLICT DO NOTHING;

    INSERT INTO service_ticket_history (
        id, tenant_id, ticket_id, actor_user_id, action, old_value, new_value, note, created_at
    )
    VALUES
        ('00000000-0000-4000-8000-000000000411', demo_tenant_id,
         '00000000-0000-4000-8000-000000000401', demo_user_id,
         'Created', NULL, 'Open', 'Chamado fictício criado para demonstração.', now() - interval '1 day'),
        ('00000000-0000-4000-8000-000000000412', demo_tenant_id,
         '00000000-0000-4000-8000-000000000402', demo_user_id,
         'Created', NULL, 'Open', 'Chamado fictício criado para demonstração.', now() - interval '3 days'),
        ('00000000-0000-4000-8000-000000000413', demo_tenant_id,
         '00000000-0000-4000-8000-000000000402', demo_user_id,
         'StatusChanged', 'Open', 'InProgress', 'Análise técnica iniciada.', now() - interval '2 days'),
        ('00000000-0000-4000-8000-000000000414', demo_tenant_id,
         '00000000-0000-4000-8000-000000000403', demo_user_id,
         'Created', NULL, 'Open', 'Chamado fictício criado para demonstração.', now() - interval '4 days'),
        ('00000000-0000-4000-8000-000000000415', demo_tenant_id,
         '00000000-0000-4000-8000-000000000403', demo_user_id,
         'StatusChanged', 'Open', 'InProgress', 'Visita técnica iniciada.', now() - interval '3 days'),
        ('00000000-0000-4000-8000-000000000416', demo_tenant_id,
         '00000000-0000-4000-8000-000000000403', demo_user_id,
         'StatusChanged', 'InProgress', 'WaitingParts', 'Aguardando peça fictícia para concluir o reparo.', now() - interval '1 day')
    ON CONFLICT DO NOTHING;

    INSERT INTO service_orders (
        id, tenant_id, ticket_id, installation_id, technician_user_id, scheduled_at,
        address, description, status, checklist, materials, created_by_user_id,
        created_at, updated_at
    )
    VALUES
        ('00000000-0000-4000-8000-000000000501', demo_tenant_id,
         '00000000-0000-4000-8000-000000000401',
         '00000000-0000-4000-8000-000000000201', demo_user_id, NULL,
         'Rua das Palmeiras, 100 - Campinas/SP',
         'Agendar diagnóstico de comunicação do inversor.',
         'Pending',
         '[{"description":"Confirmar contato com cliente","isComplete":false}]'::jsonb,
         '[]'::jsonb, demo_user_id, now() - interval '1 day', now() - interval '1 day'),
        ('00000000-0000-4000-8000-000000000502', demo_tenant_id,
         '00000000-0000-4000-8000-000000000402',
         '00000000-0000-4000-8000-000000000202', demo_user_id, now() + interval '1 day',
         'Av. do Sol, 250 - Niterói/RJ',
         'Verificar geração e revisar parâmetros do inversor.',
         'Scheduled',
         '[{"description":"Levar equipamento de medição","isComplete":false},{"description":"Revisar telemetria","isComplete":false}]'::jsonb,
         '[{"description":"Conectores MC4","quantity":2,"unit":"un"}]'::jsonb,
         demo_user_id, now() - interval '2 days', now() - interval '2 days'),
        ('00000000-0000-4000-8000-000000000503', demo_tenant_id,
         '00000000-0000-4000-8000-000000000403',
         '00000000-0000-4000-8000-000000000203', demo_user_id, now() - interval '2 hours',
         'Praça Central, 45 - Belo Horizonte/MG',
         'Inspeção do inversor e substituição de componente.',
         'InProgress',
         '[{"description":"Inspecionar conexões","isComplete":true},{"description":"Substituir componente","isComplete":false}]'::jsonb,
         '[{"description":"Componente de reposição (demo)","quantity":1,"unit":"un"}]'::jsonb,
         demo_user_id, now() - interval '1 day', now() - interval '2 hours')
    ON CONFLICT DO NOTHING;

    INSERT INTO service_order_history (
        id, tenant_id, service_order_id, actor_user_id, action, old_status, new_status, note, created_at
    )
    VALUES
        ('00000000-0000-4000-8000-000000000511', demo_tenant_id,
         '00000000-0000-4000-8000-000000000501', demo_user_id,
         'Created', NULL, 'Pending', 'Ordem fictícia criada para demonstração.', now() - interval '1 day'),
        ('00000000-0000-4000-8000-000000000512', demo_tenant_id,
         '00000000-0000-4000-8000-000000000502', demo_user_id,
         'Created', NULL, 'Scheduled', 'Ordem fictícia agendada para demonstração.', now() - interval '2 days'),
        ('00000000-0000-4000-8000-000000000513', demo_tenant_id,
         '00000000-0000-4000-8000-000000000503', demo_user_id,
         'Created', NULL, 'Scheduled', 'Ordem fictícia criada para demonstração.', now() - interval '1 day'),
        ('00000000-0000-4000-8000-000000000514', demo_tenant_id,
         '00000000-0000-4000-8000-000000000503', demo_user_id,
         'StatusChanged', 'Scheduled', 'InProgress', 'Atendimento técnico iniciado.', now() - interval '2 hours')
    ON CONFLICT DO NOTHING;

    INSERT INTO maintenance_plans (
        id, tenant_id, installation_id, name, interval_days, next_due_at, notes
    )
    VALUES
        ('00000000-0000-4000-8000-000000000601', demo_tenant_id,
         '00000000-0000-4000-8000-000000000201',
         'Inspeção semestral (Demo)', 180, CURRENT_DATE - 10,
         'Plano fictício vencido para demonstrar alertas e execução.'),
        ('00000000-0000-4000-8000-000000000602', demo_tenant_id,
         '00000000-0000-4000-8000-000000000202',
         'Limpeza e revisão preventiva (Demo)', 90, CURRENT_DATE + 14,
         'Plano fictício próximo para demonstrar agenda.')
    ON CONFLICT DO NOTHING;

    RAISE NOTICE 'Demo data added for tenant % using active user %.', demo_tenant_id, demo_user_id;
END;
$$;

COMMIT;
