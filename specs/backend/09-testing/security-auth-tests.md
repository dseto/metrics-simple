# Delta — Testing: Segurança + Auth (mínimo obrigatório)

## Backend — Testes de integração (obrigatórios)
1) Endpoint protegido sem token => 401 + ApiError + X-Correlation-Id
2) Token inválido => 401
3) Reader em endpoint Admin => 403
4) Admin em endpoint Admin => 200/201/204
5) POST /api/auth/token:
   - credenciais válidas => 200 com token
   - usuário inativo => 401
   - lockout => 429
6) Rate limiting do login => 429

## Backend — Unit tests (recomendados)
- BCrypt verify
- Lockout counters e janela
- Claim normalization para app_roles

## Frontend — smoke tests
- Login ok => token salvo e navegação
- 401 => logout + redirect /login
- Guard admin bloqueia Reader

## Critérios de aceite (resumo)
- [ ] Sem token: 401 padronizado
- [ ] Sem role: 403 padronizado
- [ ] Login via SQLite funcional
- [ ] Logs por request com sub/roles/jti/path/status/duration
- [ ] Rate limiting/lockout funcionando
- [ ] Estrutura pronta para ExternalOidc sem mudar policies
