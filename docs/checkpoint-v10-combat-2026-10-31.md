# Checkpoint v10 — resultado do combate

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

O checkpoint passa a gravar **v10** (`neutral-v10`) com o bloco `Combat`. Ele
guarda o resultado das lutas vencidas do [COM-004](combat-slice-2026-10-31.md).
Salvar deixa de ser recusado depois de uma vitória. Continua recusado só enquanto
há uma luta aberta.

```json
"Combat": {
  "Defeated": [ { "Map": 3, "Key": "<hash-do-MAP>:249676", "Pid": 16777598, "Elevation": 0, "Hex": 24508 } ],
  "Experience": 25,
  "PlayerHitPoints": 26
}
```

- **`Defeated`:** criaturas mortas pela chave técnica da origem (`hash:offset`),
  com PID, elevação e o hex onde caíram. O registro do MAP nunca muda; o mundo
  projeta o cadáver.
- **`Experience`:** XP total das lutas.
- **`PlayerHitPoints`:** HP depois da última luta. `null` quer dizer intacto. Ainda
  não existe HP fora do combate.

## Regras de restauração

A restauração é atômica: qualquer erro recusa o arquivo inteiro e mantém a sessão
viva.

- **Versão e campos:**
  - v10 exige o objeto `Combat` explícito, já na leitura do JSON.
  - v1–v9 não podem trazer `Combat`.
  - A migração de v1–v9 continua aceita e entra sem resultado de combate.
- **Cada cadáver:**
  - é único;
  - pertence a uma área restaurada;
  - corresponde a uma criatura de encontro do MAP (formiga gigante ou radscorpion),
    com o mesmo PID e a mesma elevação, sem `HIDDEN`;
  - está num hex válido que não é saída, sem bloqueadores além de criaturas e
    portas.
- **XP:** precisa ser **exatamente** a soma do XP do PRO dessas criaturas (25 por
  formiga, 60 por radscorpion). XP forjado é recusado.
- **HP:** `PlayerHitPoints` é `null` (cheio) ou fica entre 1 e o HP máximo do nível,
  exclusive. O nível é derivado do XP total restaurado (29 no nível 1, 33 no nível 2,
  ver [progressão](character-progression-2026-10-31.md)).
- **Ordem:** o bloco é restaurado depois de áreas, portas, itens, cenário, Templo e
  entradas.

## Coordenação com a Frente A

O roadmap reservava o v10 para a CONT-003 (estado da Anciã de Arroyo), que ainda
não foi iniciada em nenhum worktree. O v10 entrou primeiro com o combate.

- Se a Frente A ainda não tiver criado a própria versão, o estado da Anciã pode entrar como **v11**.
- Se as duas frentes forem integradas juntas, os dois campos podem ser somados num mesmo número de versão.

As duas mudanças só tocam campos novos e independentes.

## Arquivos

- `src/Runtime/WorldCheckpoint.cs`: campo `Combat` e exigência explícita em v10.
- `src/Runtime/WorldSession.cs`: gravação em v10, aceitação de v10 e regra de `Combat` por versão.
- `src/Runtime/WorldCombat.cs`:
  - `CaptureCombat` e `RestoreCombat`;
  - `HasCombatOutcome`, que substitui `CombatOutcomeUnsaved`;
  - `DefeatedCritter` com campos obrigatórios no JSON.
- `src/Preview/CheckpointPreview.cs`: o salvamento depois de vitórias foi liberado.
- **Testes:** as fixtures legadas também removem `Combat`, e as asserções da versão
  gravada passaram de 9 para 10 (testes sintéticos e checagens do preview).

## Validação

- **Testes sintéticos:** 354 grupos. O novo grupo cobre:
  - ida e volta pelo arquivo;
  - bloco vazio;
  - migração de v9;
  - 11 checkpoints forjados recusados: XP, HP cheio e zero, duplicata, chave, PID,
    elevação, hex de parede, mapa, `Combat` ausente e v9 com `Combat`;
  - JSON v10 sem `Combat`.
- **`--preview-test` (`COMBAT_SLICE_OK`):** a luta real é vencida. O test confere
  que o arquivo traz v10 com a formiga, o hex e o XP. Reiniciar devolve a formiga;
  carregar recoloca o cadáver, os 25 XP e o HP. Salvar no meio da luta não grava
  nada.
- **`tools/Validate-Temple.ps1`:** 21 passos aprovados.
