-- Usuários da retaguarda (gerente, estoquista...). Senha só como hash (PBKDF2 do ASP.NET Identity).
CREATE TABLE backoffice_users (
    id            uuid        PRIMARY KEY,
    username      text        NOT NULL,
    display_name  text        NOT NULL,
    password_hash text        NOT NULL,
    role          text        NOT NULL DEFAULT 'gerente',
    active        boolean     NOT NULL DEFAULT true,
    created_at    timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_backoffice_users_username ON backoffice_users (lower(username));
