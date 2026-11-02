# Status do Projeto — FalloutRemake

> **Data da Auditoria:** 02/11/2026  
> **Branch Ativa:** `codex/arroyo-elder-entry-audit`  
> **Commit Base Examinado:** `fe99d18` (*"[INTEGRATION] Merge de agent/claude-work (6e9ce96) e Argon CONT-001..004 (815274c) com atualizacao de caminhos D:\Dev"*)  
> **Referência Operacional Obrigatória:** [AGENTS.md](../AGENTS.md)

---

## 1. Visão Geral e Escopo

O **FalloutRemake** é uma recriação pessoal e não comercial de Fallout 2 em **Godot 4.7 .NET / C#**, adotando uma apresentação isométrica 3D com iluminação e qualidade de materiais inspiradas em *Wasteland 3*, preservando as estruturas binárias e regras clássicas do motor original.

### Escopo Ativo Atual
O desenvolvimento concentra-se no prólogo:
1. **Temple of Trials (Templo dos Ensaios):**
   - Interior: `maps/arcaves.map` (Map ID 3) — Elevações 0, 1 e 2.
   - Exterior: `maps/artemple.map` (Map ID 126).
2. **Transição para a Aldeia de Arroyo:**
   - Mapa: `maps/arvillag.map` (Map ID 4) — Saída pela porta 100, concessão única de recompensa de Speech e aproximação/diálogo com a Anciã (*Elder*).

### Referência Técnica
O projeto utiliza como referência técnica as regras, tabelas binárias e opcodes do **Fallout 2 Community Engine (CE)** (revisão `e97087b`), garantindo paridade em algoritmos de linha de visão, ocupação de hexágonos, estruturas de mapas e decodificação de scripts SSL compilados (`.int`).

---

## 2. Resumo da Arquitetura e Decoupling

O código em `src/` adota uma divisão estrita entre a lógica de domínio e a camada de motor:

```text
src/
├── Import/    (29 arquivos) -> C# Puro (.NET 8). Decodificação de formatos clássicos (DAT, MAP, PRO, INT, GAM, MSG, FRM, GCD, AI).
├── Geometry/  (11 arquivos) -> C# Puro (.NET 8). Matemática de grid hex (40.000 hexes), BFS, pathfinder CE, linha reta CE, oclusão de paredes, enquadramento.
├── Runtime/   (64 arquivos) -> C# Puro (.NET 8). Máquina de estados (WorldSession), combate por turnos CE, ficha/perks/descanso/medicina, checkpoints v1-v13, variáveis transacionais.
└── Preview/   (114 arquivos)-> Godot 4 Presentation. TemplePreview + janelas HUD e runners de teste in-engine.
```

O projeto de console [tools/TempleInspector/TempleInspector.csproj](../tools/TempleInspector/TempleInspector.csproj) referencia exclusivamente `Import`, `Geometry` e `Runtime`, executando testes sintéticos sem qualquer dependência ou inicialização do Godot.

---

## 3. Matriz de Estado dos Sistemas

Para evitar falsos positivos de desenvolvimento, os subsistemas são classificados sob categorias rigorosas:

1. **Lógica de Domínio Implementada:** Classes e algoritmos de regra de negócio, parsing ou cálculos concluídos no Core C#.
2. **Lógica Coberta por Testes Sintéticos / Ensaios:** Validação algorítmica comprovada via suíte unitária de console ou ensaios desacoplados em memória.
3. **Fluxo Integrado à Sessão e Persistido:** Regra conectada ao ciclo de vida do `WorldSession` e preservada em checkpoints `v1`–`v13`.
4. **Fluxo Exercitável no Protótipo de Apresentação:** Ação que pode ser acionada interativamente na cena de teste (`TemplePreview.tscn`) por cliques ou atalhos.
5. **Gameplay Integrado de Ponta a Ponta:** Fluxo de jogo final, com loops de quest interativos, combate, diálogos cinematográficos e feedback visual completo. *(Atualmente, nenhum sistema possui status de gameplay final integrado).*

### Tabela de Classificação dos Subsistemas

| Subsistema | Classificação Real | Evidências (Arquivos / Classes) | O que está efetivamente comprovado |
| :--- | :--- | :--- | :--- |
| **Leitura de Binários Clássicos** | **Lógica de Domínio + Coberta por Testes** | [DatArchive.cs](../src/Import/DatArchive.cs), [MapDocument.cs](../src/Import/MapDocument.cs), [CritterPrototypeData.cs](../src/Import/CritterPrototypeData.cs), [WeaponPrototypeData.cs](../src/Import/WeaponPrototypeData.cs), [ArmorPrototypeData.cs](../src/Import/ArmorPrototypeData.cs), [AmmoPrototypeData.cs](../src/Import/AmmoPrototypeData.cs), [DrugPrototypeData.cs](../src/Import/DrugPrototypeData.cs), [CharacterFile.cs](../src/Import/CharacterFile.cs), [AiPacketCatalog.cs](../src/Import/AiPacketCatalog.cs), [IntProgram.cs](../src/Import/IntProgram.cs), [MapLayoutWriter.cs](../src/Import/MapLayoutWriter.cs) | Decodificação determinística de arquivos DAT, PRO (criaturas, armas, armaduras, munições, drogas), GAM, MSG, FRM, GCD (`premade/*.gcd`), `ai.txt` e opcodes INT, além de validação estrutural round-trip e detecção de corrupção via [MapLayoutWriter.cs](../src/Import/MapLayoutWriter.cs), comprovadas pelos 476 grupos de testes sintéticos. A identidade byte a byte (`SequenceEqual`) contra os binários originais de `maps/arcaves.map` e `maps/arvillag.map` é comprovada separadamente pelos relatórios CLI e pelo `Validate-Temple.ps1` via `MapLayoutWriter.Verify`. |
| **Grid Hexagonal e Pathfinding** | **Lógica de Domínio + Exercitável no Protótipo** | [FalloutGrid.cs](../src/Geometry/FalloutGrid.cs), [HexNavigation.cs](../src/Geometry/HexNavigation.cs), [OriginalPathfinder.cs](../src/Geometry/OriginalPathfinder.cs), [OriginalStraightPath.cs](../src/Geometry/OriginalStraightPath.cs) | Conversão 3D/Hex em 40.000 hexes, BFS e pathfinder do CE respeitando flags `NO_BLOCK`, `MULTIHEX`, linha de visão/tiro e elevações. |
| **Sessão e Checkpoints (v1 a v13)** | **Integrado à Sessão e Persistido** | [WorldSession.cs](../src/Runtime/WorldSession.cs), [WorldCheckpoint.cs](../src/Runtime/WorldCheckpoint.cs), [AtomicFileReplacement.cs](../src/Runtime/AtomicFileReplacement.cs) | Serialização e restauração atômica de JSON multi-mapa. Migração determinística comprovada de saves legados (`v1` a `v12`). Preserva portas, itens do chão, inventário, combate, personagem, duelo, perícias/perks, munição/cargas de armas e estado de diálogo da Anciã (`SavedElderState`). |
| **Portas, Chaves e Arrombamento** | **Integrado à Sessão e Exercitável no Protótipo** | [TempleDoorAdapter.cs](../src/Runtime/TempleDoorAdapter.cs), [DoorSession.cs](../src/Runtime/DoorSession.cs) | Abertura/fechamento e arrombamento da 2ª porta concedendo 25 XP únicos (Lockpick). Abertura da porta 100 da vila via tecla `U` após resolução de Speech, duelo desarmado ou chave do corpo de Cameron. |
| **Explosivo e Destruição de Barreira** | **Integrado à Sessão e Exercitável no Protótipo** | [ExplosiveTimer.cs](../src/Runtime/ExplosiveTimer.cs), [TempleBarrierProjection.cs](../src/Runtime/TempleBarrierProjection.cs) | Posicionamento de dinamite pela interface de bolsa, relógio lógico (+1s/+10s), detonação atômica com remoção do bloqueador da barreira e passagem física liberada para a elevação 2. |
| **Inventário, Peso e Contêineres** | **Integrado à Sessão e Exercitável no Protótipo** | [ItemTransferSession.cs](../src/Runtime/ItemTransferSession.cs), [CarryWeight.cs](../src/Runtime/CarryWeight.cs), [ItemOriginRoute.cs](../src/Runtime/ItemOriginRoute.cs) | Coleta em massa ("Take All") e devolução integral ("Return All") com checagem estrita de peso por Força. Itens retornados voltam ao contêiner de origem sem duplicar. |
| **Speech do Templo (Cameron) e Partida do NPC** | **Ensaio Desacoplado com Integração Opcional à Sessão** | [TempleSpeechDraft.cs](../src/Runtime/TempleSpeechDraft.cs), [TempleNpcDeparture.cs](../src/Runtime/TempleNpcDeparture.cs), [WorldTempleSpeech.cs](../src/Runtime/WorldTempleSpeech.cs), [TempleActivePreview.cs](../src/Preview/TempleActivePreview.cs) | Ensaio isolado da rota diplomática de 5 escolhas de Cameron (`scripts/actemvil.int`) em janela de texto, com integração explícita via HUD (`CommitTempleSpeech`) e persistência em `WorldCheckpoint` (schema v6+). |
| **Transição para Arroyo e Recompensas** | **Integrado à Sessão e Validado em Teste Headless** | [TempleVillageRoute.cs](../src/Runtime/TempleVillageRoute.cs), [ArroyoRewardProfile.cs](../src/Runtime/ArroyoRewardProfile.cs), [TempleVillageChecks.cs](../src/Preview/TempleVillageChecks.cs) | Rota até a porta 100, travessia com tecla `Enter` para o mapa 4, concessão de XP (600 XP na rota diplomática/desarmada ou 300 XP ao matar Cameron) e persistência de Karma e Reputação. |
| **Assets 3D de Natureza e Arquitetura do Templo** | **Exercitável no Protótipo Visual** | [NatureModels.cs](../src/Preview/NatureModels.cs), [NaturePicking.cs](../src/Preview/NaturePicking.cs), [TempleArchitecture.cs](../src/Geometry/TempleArchitecture.cs), [VisualPreferences.cs](../src/Runtime/VisualPreferences.cs) | Modelos GLB otimizados de rochas e árvores queimadas e kit modular glTF do Templo dos Ensaios substituem primitivas na cena, com picking 3D preciso e persistência de preferências visuais v2. |
| **Interação com a Anciã de Arroyo** | **Ensaio Desacoplado (1º Diálogo + Conversa Repetida `Node004`) com Integração à Sessão e Persistência (`v10+`)** | [ArroyoElderConsultation.cs](../src/Runtime/ArroyoElderConsultation.cs), [ArroyoElderStateProfile.cs](../src/Runtime/ArroyoElderStateProfile.cs), [ArroyoElderWindow.cs](../src/Preview/ArroyoElderWindow.cs), [ArroyoElderConsultationPreview.cs](../src/Preview/ArroyoElderConsultationPreview.cs), [ArroyoElderConsultationChecks.cs](../src/Preview/ArroyoElderConsultationChecks.cs), [WorldSession.cs](../src/Runtime/WorldSession.cs), [WorldCheckpoint.cs](../src/Runtime/WorldCheckpoint.cs) | Ensaio interativo do primeiro diálogo (`Node001 → Node999`) e da conversa repetida (`Node004 → Node013 / Node999`, com opção 118 de Vault 13 desabilitada enquanto `metarule(17, 11) == 0`). Aplicação transacional explícita à `WorldSession` tanto do primeiro diálogo (`CommitElderDialogue`, gravando `GVAR_TALKED_TO_ELDER (531) = 1` e locais `L0..L4`) quanto da conversa repetida (`CommitElderRepeatDialogue`, atualizando reação e ajuste acumulado via `Node013`), com persistência e restauração completas em `SavedElderState` (`Choices` e `RepeatChoices`) no checkpoint (`v10`–`v13`). |
| **Apresentação Visual / HUD** | **Protótipo de Testes** | [TemplePreview.cs](../src/Preview/TemplePreview.cs), [PreviewHud.cs](../src/Preview/PreviewHud.cs) | Cena isométrica ortográfica de inspeção e validação funcional com corte de oclusão e janelas modais de ensaio/ficha/combate. |
| **Demais Categorias de Assets 3D** | **Parcial (Importados, Não Mapeados na Cena)** | Pastas em `Assets/Containers`, `Assets/Furniture`, `Assets/Props` | Modelos GLB otimizados importados com `.import` válidos, aguardando mapeamento na cena para substituir objetos restantes. |
| **Combate por Turnos e Ações** | **Integrado à Sessão, Persistido e Exercitável no Protótipo** | [CombatSession.cs](../src/Runtime/Combat/CombatSession.cs), [WorldCombat.cs](../src/Runtime/WorldCombat.cs), [WorldTempleDuel.cs](../src/Runtime/WorldTempleDuel.cs), [WorldFirearms.cs](../src/Runtime/WorldFirearms.cs), [WorldThrow.cs](../src/Runtime/WorldThrow.cs), [CombatViewAdapter.cs](../src/Preview/CombatViewAdapter.cs) | Encontros por linha de visão CE, AP/ToHit/DT/DR do CE, `combat_ai` com pacotes de `ai.txt`, Healing Powder em combate (4 AP), desafio desarmado e morte de Cameron, duas mãos ativas + armadura e perks de armadura, requisito de Força e One Hander, tiros mirados (*called shots*), arremesso de lança e itens no chão, veneno de radscorpion, movimentação autônoma de criaturas, críticos do CE, armas de fogo (munição, recarga, rajada/dispersão, perks de arma) e rastreamento de cargas/pilhas de munição no mundo (checkpoint `v13`). |
| **Progressão, Ficha, Perks, Descanso e Medicina** | **Integrado à Sessão, Persistido e Exercitável no Protótipo** | [PlayerCharacter.cs](../src/Runtime/PlayerCharacter.cs), [CharacterFile.cs](../src/Import/CharacterFile.cs), [CharacterProgression.cs](../src/Runtime/CharacterProgression.cs), [WorldSkills.cs](../src/Runtime/WorldSkills.cs), [Perks.cs](../src/Runtime/Perks.cs), [WorldRest.cs](../src/Runtime/WorldRest.cs), [WorldMedicine.cs](../src/Runtime/WorldMedicine.cs) | Premades (`Narg`, `Mingan`, `Chitsa`) lidos dos `.gcd` originais; criação de personagem (40 pontos, até 2 traits e 3 tags); progressão de níveis por XP; gasto de pontos de perícia com custos do CE; Perks (`Lifegiver`, `Educated`, `Tag!`, `Mutate!`, `Here and Now`, `Swift Learner`); descanso (`pipboyRest`) e perícias médicas (`First Aid` e `Doctor` com kits e limite diário), todos persistidos no checkpoint `v13`. |
| **Build Android / Mobile** | **Não Verificado** | [Fallout Remake.csproj](../Fallout%20Remake.csproj#L4) | Existe declaração condicional de `net9.0` para Android, mas sem automação ou testes que comprovem a execução mobile. |

---

## 4. Distinção Crucial: O que é o Protótipo Atual

O projeto atual **não é uma fatia vertical jogável de RPG comercial**. Trata-se de uma **cena de inspeção interativa e bancada de validação de regras** (`TemplePreview`):

1. **Sobre o Percurso Templo $\to$ Arroyo:**
   - **O que está comprovado:** No protótipo de teste headless ([TempleVillageChecks.cs](../src/Preview/TempleVillageChecks.cs)), a máquina de estados `WorldSession` é exercitada através de comandos simulados (botões de rota no HUD, tecla `U` para porta 100, tecla `Enter` na saída). O sistema transporta o personagem para o mapa 4 (`arvillag.map`), credita a recompensa de entrada uma única vez e salva o checkpoint com Karma e Reputação de Arroyo, verificando inclusive o retorno ao Templo sem duplicação de XP.
   - **O que NÃO existe:** Não há transição cinemática, não há salvamento de quests com interface de Pip-Boy, não há diálogos com outros habitantes da aldeia além da Anciã e a caminhada fora de combate ocorre por interpolação linear.
2. **Sobre Ensaios Desacoplados vs. Sessão:**
   - **Ensaio Desacoplado com Ponte de Integração (Cameron e Anciã):** Tanto o diálogo diplomático de Cameron ([TempleSpeechDraft](../src/Runtime/TempleSpeechDraft.cs) $\to$ `WorldSession.CommitTempleSpeech`) quanto o diálogo com a Anciã de Arroyo ([ArroyoElderConsultation](../src/Runtime/ArroyoElderConsultation.cs) / [ArroyoElderStateProfile](../src/Runtime/ArroyoElderStateProfile.cs) $\to$ `WorldSession.CommitElderDialogue` e `WorldSession.CommitElderRepeatDialogue`) iniciam como ensaios de consulta que não alteram a partida automaticamente durante a leitura. A promoção para a sessão viva ocorre exclusivamente mediante ação explícita de confirmação no HUD (`ApplyRequested` / `CommitElderDialogueRequested`), momento em que o estado validado (`GVAR_TALKED_TO_ELDER = 1` e locais do ator) é gravado na `WorldSession` e persistido em `WorldCheckpoint` (`SavedElderState`, schema `v10`–`v13`).

---

## 5. Suítes de Teste e Limites Reais de Comprovação

### A. Testes Sintéticos Unitários (`tools/TempleInspector`)
* **Comando:** `dotnet run --project "tools/TempleInspector/TempleInspector.csproj" -- --self-test`
* **Tempo:** Medido em ~2 segundos (portável, sem Godot, sem arquivos externos).
* **O que comprova:** **476 grupos de teste** executados sobre fixtures sintéticas em memória (`Fixture`), cobrindo parsers binários, validação estrutural round-trip e detecção de corrupção via `MapLayoutWriter.Verify` ([StructuralTests.cs](../tools/TempleInspector/StructuralTests.cs#L22,L149)), regras do grid hex e pathfinder CE, combate por turnos, armas de fogo, criação/progressão de personagem, diálogos de Cameron e da Anciã (`Node001` e `Node004`, incluindo persistência da conversa repetida) e migração de checkpoints `v1` a `v13`.
* **O que NÃO comprova:** Não acessa os arquivos originais da instalação clássica (portanto, a suíte sintética isolada **não comprova** por si só a identidade byte a byte dos mapas reais em disco), não executa nós do Godot, não testa áudio, física ou compatibilidade de shaders.

### B. Relatórios de Conformidade de Dados
* **Comando:** `dotnet run --project tools/TempleInspector/TempleInspector.csproj -- --data-dir "<fallout2>" --<nome>-report`
* **Tempo Estimado:** ~0,5s por relatório.
* **O que comprova:** Conformidade estrita de offsets, tabelas e estruturas nos relatórios JSON contra os arquivos originais da instalação clássica. **É nesta etapa** que `MapLayoutWriter.Verify(map)` ([MapLayoutWriter.cs#L83-L87](../src/Import/MapLayoutWriter.cs#L83-L87)) lê os arquivos reais (`maps/arcaves.map` e `maps/arvillag.map`) da instalação, reconstrói os binários a partir dos campos interpretados e executa a comparação explícita byte a byte (`SequenceEqual`) contra os bytes brutos lidos da instalação, emitindo `"OriginalReconstruction": "verified"`.

### C. Validação Integrada Headless (`tools/Validate-Temple.ps1`)
* **Comando:** `powershell -File tools/Validate-Temple.ps1 -GodotExe "<godot-console.exe>" -DataDirectory "<fallout2>"`
* **O que comprova:** Compilação C#, 476 testes sintéticos, relatórios JSON e execução in-engine em modo `--headless` dos testes assíncronos (`--nature-test`, `--explosive-preparation-test` e `--preview-test`, totalizando 21/21 etapas). O validador confirma saída sem exceções não tratadas e ausência de mensagens com severidade ERROR ou WARNING registradas no console do Godot.
* **O que NÃO comprova (Limitações Críticas):**
  - **NÃO comprova fidelidade visual ou ausência de falhas gráficas:** Por rodar em modo `--headless`, nenhum pipeline de rasterização ou apresentação na tela é testado.
  - **NÃO comprova ausência de vazamentos de memória:** Nenhum profiler de heap ou análise estrita de alocação de longo prazo é executado; apenas confirma que o processo encerra sem quebra de execução.

---

## 6. Próximo Marco Oficial (Frente A)

Com a conclusão e integração dos pacotes `CONT-001` a `CONT-005` (incluindo a confirmação e persistência da conversa repetida `Node004` em `WorldSession.CommitElderRepeatDialogue` e `SavedElderState.RepeatChoices`):

* **Próximo Passo Imediato (`CONT-006`):** Auditar e habilitar os ramos de entrega da quest principal na Anciã (`Node007`, `Node014` e `Node015` de `scripts/ahelder.int` — entrega do cantil da Vault 13 `PID 267`, concessão de `100` Caps, marcação de Klamath `area 1` como conhecida e gravação de `L7 = 1`, destravando a opção pós-quest `120 → Node009` em `Node004`).
