# Sétimo marco — adaptador de arrombamento

Branch `codex/temple-lockpick-adapter`, base `c2deaff`. Implementação restrita das
condições [mapeadas no marco anterior](door-script-2026-10-18.md).

## Comportamento

`DoorSession.FromMap(map, enableTempleDoorAdapter: true)` habilita explicitamente
o adaptador para a instância de offset 137396, ID 102, hex 11108. Confere hashes e
tamanhos do MAP/INT, caminho, SID, índice, vínculo com o dono e ausência de script
adicional no protótipo. Recebe identidades do importador já validado; não é uma
nova autenticação de estruturas arbitrárias criadas manualmente.

Um perfil desconhecido é recusado. O caminho padrão `FromMap(map)` continua
recusando o uso desta porta como script não implementado; outras portas com scripts
não recebem a exceção. IDs duplicados não identificam a instância.

`TempleDoorAdapter` possui oito locais separados da fonte. Na entrada, L3=1, L0=0
e L1=1: porta trancada, armadilha inativa. Reentrar preserva os locais resolvidos e
sincroniza a tranca. `Reset` restaura as portas, reinicializa os locais e zera a
recompensa da sessão. Snapshots dos locais são cópias somente leitura.

`UseLockpick` recebe uma função que resolve `LockpickRequest`: perícia 9,
modificador **+100**, ação destrancar ou retrancar conforme L0. O adaptador não
calcula probabilidades nem inventa atributos para o marcador.

| Resultado recebido | Efeito ao destrancar |
| --- | --- |
| Sucesso / sucesso crítico | L0=1, remove `LOCKED`, mantém a porta fechada; 25 XP se L5=0. |
| Falha | Preserva tranca, locais e XP. |
| Falha crítica | Acrescenta `JAMMED`; novas tentativas são recusadas antes da resolução. |

Usar Lockpick novamente quando L0=1 tenta **retrancar**. Sucesso faz L0=0 e tranca,
usando o mesmo L5 para não repetir XP. Falha crítica ao retrancar **não emperra**:
o procedimento `Lock_Door` não faz essa chamada. O script permite retrancar uma
porta aberta; o adaptador preserva esse estado e os bits alheios à tranca.

Flags: `LOCKED=0x02000000`, `JAMMED=0x04000000`. Destrancar não abre nem libera a
navegação; a abertura continua explícita e impede fechamento sobre ocupantes.
O uso normal adaptado só cobre o estado sem armadilha ativa confirmado na entrada.

Tentativas exigem proximidade, mesma elevação e marcador parado fora de saídas.
Porta emperrada não consulta a função de resolução. Exceção ou resultado inválido
da função não aplica efeitos; resolução reentrante é recusada. Se a sessão mudar
durante a função, o resultado antigo retorna `StateChanged` e é descartado.
`Ready` indica tentativa aceita; sucesso/falha estão no campo `Outcome`.

## Validação reproduzível

```powershell
dotnet run --project tools/TempleInspector -- --self-test
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --door-adapter-test
```

O segundo comando aplica resultados **determinísticos** aos dados reais e emite
`TEMPLE_ADAPTER_TEST_OK`, com saída 0. Não executa INT nem seleciona resultados no jogo.

- **57 grupos sintéticos**: 13 novos para identificação, inicialização, resultados,
  emperramento, retranca, XP único, reinício, isolamento e funções de resolução.
- MAP Epic original: aproximação de 23 passos à primeira porta e 71 passos da
  primeira aproximação à segunda, respeitando bloqueios e saídas terminais.
- Alcance: **540 → 2184 → 2189** hexes com as portas fechadas, primeira aberta e
  ambas abertas. Destrancar sozinho mantém 2184. As saídas continuam terminais.
- Falha e emperramento, sucesso e 25 XP, abertura, travessia, recusa de fechamento
  ocupado, fechamento, retranca e nova tentativa sem XP adicional foram validados.
- Reentrada preserva a solução. Reinício restaura a tranca inicial e 540 hexes.
- Reconstrução byte a byte do MAP antes/depois; flags, posições e IDs fonte preservados.
- Compilação Godot .NET sem avisos/erros; regressão da prévia sem janela aprovada.

Logs e relatório ficam em `.local/adapter-*.log` e `.local/temple-adapter-scenario.json`.
Não há dados originais, programas extraídos ou capturas novos no Git.

## Limite e próximo passo

A [integração na prévia](lockpick-preview-2026-10-18.md) foi entregue no oitavo
marco, com perfil de teste explícito e rolagem. O texto abaixo registra o limite
ao concluir este sétimo marco.

A interface da prévia mantém a segunda porta indisponível. O adaptador está pronto
e testável pelo cenário; a integração jogável depende de definir o perfil mínimo
do personagem e a resolução de perícia. XP aqui representa recompensa acumulada
desta sessão, não um sistema completo de progressão ou save.

Não foram implementados ferramenta de PID 84, dano, armadilhas ativas, destruição,
tempo para desimpedir fechaduras ou VM geral. Ainda falta comparação jogável com o
original. Próximo: definir o perfil de personagem usado no protótipo, conferir o
fluxo no original e ligar uma ação explícita de Lockpick na prévia.

Referência CE local: commit `e97087b9582f37075db347a89898887320753f8b`,
`proto_instance.cc` (`objectLock`, `objectUnlock`, `objectJamLock`, `_obj_use_skill_on`)
e `obj_types.h`, além do bytecode já analisado.
