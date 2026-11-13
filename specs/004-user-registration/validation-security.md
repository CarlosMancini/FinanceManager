# Validação de segurança · 004

## T050 — parcial

- `RegistrationPrivacyTests` passou em SQL Server LocalDB: cadastro e estado público não expuseram perfil, senha ou CSRF nas respostas e mensagens de log capturadas; a auditoria de cadastro registrou ação, titular e instante UTC. A prova legada retornou token somente no cookie, com Secure, HttpOnly e SameSite=Strict; corpo e logs capturados não continham token, credenciais, CSRF ou descrição financeira sintética.
- A revisão de `Program.cs` identificou restrição de endereço HTTPS em localhost e `ListenLocalhost`. `SessionAdmissionFilter` valida antiforgery em POST, PUT, PATCH e DELETE. Os testes de contrato existentes verificam atributos de cookies, CSRF, sessão e autorização restrita. A leitura do login em `IdentityOwnerAdapter` consulta o titular; a recuperação da sessão usa apenas sessão e e-mail, sem carregar histórico financeiro.
- Ainda falta completar a inspeção sistemática de logs/respostas em todos os cenários e mutações, incluindo payload financeiro, para concluir T050. As evidências acima não comprovam ausência de vazamento em todos os caminhos.
