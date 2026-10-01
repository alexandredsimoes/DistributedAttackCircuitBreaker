# Distributed Attack Circuit Breaker

Implementação C#/.NET 9 de um circuit breaker distribuído para detecção de ataques usando Redis.

## Características
- Sliding window com Redis Sorted Set.
- Lua Script atômico para registrar ataque, remover eventos expirados e avaliar threshold.
- Estado global `Closed`, `Open`, `HalfOpen`.
- Lock distribuído Redis (`SET NX PX`) para garantir que apenas uma instância execute a checagem periódica.
- `BackgroundService` com renovação do lease e verificação do circuito.
- Testes unitários e testes de integração com Redis real via Testcontainers.

## Política de exemplo
- 10 ataques em 60 segundos => `Open`.
- 30 segundos de cooldown => `HalfOpen`.
- 1 falha no probe => `Open` novamente.
- 3 probes bem-sucedidos => `Closed`.

## Execução

```bash
dotnet test
```

Os testes de integração precisam de Docker e usam `Testcontainers.Redis`.
