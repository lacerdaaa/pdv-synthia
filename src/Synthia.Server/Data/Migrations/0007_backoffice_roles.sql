-- Papéis da retaguarda: admin (tudo), gerente (tudo menos usuários), estoquista (estoque, sem preços).
-- O admin criado no primeiro start nasceu como 'gerente'; passa a ser 'admin'.
UPDATE backoffice_users SET role = 'admin' WHERE lower(username) = 'admin';
ALTER TABLE backoffice_users ADD CONSTRAINT ck_backoffice_users_role CHECK (role IN ('admin', 'gerente', 'estoquista'));
