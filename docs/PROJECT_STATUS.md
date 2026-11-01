# Status do Projeto — FalloutRemake

> **Data da Auditoria:** 22/10/2026  
> **Branch Ativa:** `codex/arroyo-elder-entry-audit`  
> **Commit Base Examinado:** `a66f31c` (*"Verify changed elder sources are refused and record the nine-milestone batch"*)  
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
   - Mapa: `maps/arvillag.map` (Map ID 4) — Saída pela porta 100, concessão única de recompensa de Speech e aproximação da Anciã (*Elder*).

### Referência Técnica
O projeto utiliza como referência técnica as regras, tabelas binárias e opcodes do **Fallout 2 Community Engine (CE)** (revisão `e97087b`), garantindo paridade em algoritmos de linha de visão, ocupação de hexágonos, estruturas de mapas e decodificação de scripts SSL compilados (`.int`).

---

## 2. Resumo da Arquitetura e Decoupling

O código em `src/` adota uma divisão estrita entre a lógica de domínio e a camada de motor:

```text
src/
├── Import/    (23 arquivos) -> C# Puro (.NET 8). Decodificação de formatos clássicos (DAT, MAP, PRO, INT, GAM, MSG, FRM).
├── Geometry/  (8 arquivos)  -> C# Puro (.NET 8). Matemática de grid hex (40.000 hexes), BFS, oclusão de paredes, enquadramento.
├── Runtime/   (35 arquivos) -> C# Puro (.NET 8). Máquina de estados (WorldSession), checkpoints v1-v9, variáveis transacionais, regras de quest.
└── Preview/   (84 arquivos) -> Godot 4 Presentation. TemplePreview (70 arquivos parciais) + 14 janelas HUD e runners de teste in-engine.
```

O projeto de console [tools/TempleInspector/TempleInspector.csproj](../tools/TempleInspector/TempleInspector.csproj) referencia exclusivamente `Import`, `Geometry` e `Runtime`, executando testes sintéticos sem qualquer dependência ou inicialização do Godot.

---

## 3. Matriz de Estado dos Sistemas

Para evitar falsos positivos de desenvolvimento, os subsistemas são classificados sob categorias rigorosas:

1. **Lógica de Domínio Implementada:** Classes e algoritmos de regra de negócio, parsing ou cálculos concluídos no Core C#.
2. **Lógica Coberta por Testes Sintéticos / Ensaios:** Validação algorítmica comprovada via suíte unitária de console ou ensaios desacoplados em memória.
3. **Fluxo Integrado à Sessão e Persistido:** Regra conectada ao ciclo de vida do `WorldSession` e preservada em checkpoints `v1`–`v9`.
4. **Fluxo Exercitável no Protótipo de Apresentação:** Ação que pode ser acionada interativamente na cena de teste (`TemplePreview.tscn`) por cliques ou atalhos.
5. **Gameplay Integrado de Ponta a Ponta:** Fluxo de jogo final, com loops de quest interativos, combate, diálogos cinematográficos e feedback visual completo. *(Atualmente, nenhum sistema possui status de gameplay final integrado).*

### Tabela de Classificação dos Subsistemas

| Subsistema | Classificação Real | Evidências (Arquivos / Classes) | O que está efetivamente comprovado |
| :--- | :--- | :--- | :--- |
| **Leitura de Binários Clássicos** | **Lógica de Domínio + Coberta por Testes** | [DatArchive.cs](../src/Import/DatArchive.cs), [MapDocument.cs](../src/Import/MapDocument.cs), [CritterPrototypeData.cs](../src/Import/CritterPrototypeData.cs), [IntProgram.cs](../src/Import/IntProgram.cs), [MapLayoutWriter.cs](../src/Import/MapLayoutWriter.cs) | Decodificação determinística de arquivos DAT, PRO, GAM, MSG, FRM e opcodes INT, além de validação estrutural round-trip e detecção de corrupção via [MapLayoutWriter.cs](../src/Import/MapLayoutWriter.cs), comprovadas pelos 335 testes sintéticos. A identidade byte a byte (`SequenceEqual`) contra os binários originais de `maps/arcaves.map` e `maps/arvillag.map` é comprovada separadamente pelos relatórios CLI (`--temple-state-report`, etc.) e pelo `Validate-Temple.ps1` via `MapLayoutWriter.Verify`. |
| **Grid Hexagonal e Pathfinding** | **Lógica de Domínio + Exercitável no Protótipo** | [FalloutGrid.cs](../src/Geometry/FalloutGrid.cs), [HexNavigation.cs](../src/Geometry/HexNavigation.cs) | Conversão 3D/Hex em 40.000 hexes. BFS respeitando flags `NO_BLOCK`, `MULTIHEX` e elevações. No protótipo visual, o marcador do personagem move-se por interpolação a 5 passos/s sem consumo de Action Points. |
| **Sessão e Checkpoints (v1 a v9)** | **Integrado à Sessão e Persistido** | [WorldSession.cs](../src/Runtime/WorldSession.cs), [WorldCheckpoint.cs](../src/Runtime/WorldCheckpoint.cs), [AtomicFileReplacement.cs](../src/Runtime/AtomicFileReplacement.cs) | Serialização e restauração atômica de JSON multi-mapa. Migração determinística comprovada de saves legados (`v1` a `v8`). Preserva portas, itens do chão, inventário e variáveis de script auditadas. |
| **Portas, Chaves e Arrombamento** | **Integrado à Sessão e Exercitável no Protótipo** | [TempleDoorAdapter.cs](../src/Runtime/TempleDoorAdapter.cs), [DoorSession.cs](../src/Runtime/DoorSession.cs) | Abertura/fechamento e arrombamento da 2ª porta concedendo 25 XP únicos (Lockpick). Abertura da porta 100 da vila via tecla `U` após resolução de Speech. |
| **Explosivo e Destruição de Barreira** | **Integrado à Sessão e Exercitável no Protótipo** | [ExplosiveTimer.cs](../src/Runtime/ExplosiveTimer.cs), [TempleBarrierProjection.cs](../src/Runtime/TempleBarrierProjection.cs) | Posicionamento de dinamite pela interface de bolsa, relógio lógico (+1s/+10s), detonação atômica com remoção do bloqueador da barreira e passagem física liberada para a elevação 2. |
| **Inventário, Peso e Contêineres** | **Integrado à Sessão e Exercitável no Protótipo** | [ItemTransferSession.cs](../src/Runtime/ItemTransferSession.cs), [CarryWeight.cs](../src/Runtime/CarryWeight.cs), [ItemOriginRoute.cs](../src/Runtime/ItemOriginRoute.cs) | Coleta em massa ("Take All") e devolução integral ("Return All") com checagem estrita de peso por Força. Itens retornados voltam ao contêiner de origem sem duplicar. |
| **Speech do Templo (Cameron) e Partida do NPC** | **Ensaio Desacoplado com Integração Opcional à Sessão** | [TempleSpeechDraft.cs](../src/Runtime/TempleSpeechDraft.cs), [TempleNpcDeparture.cs](../src/Runtime/TempleNpcDeparture.cs), [WorldTempleSpeech.cs](../src/Runtime/WorldTempleSpeech.cs), [TempleActivePreview.cs](../src/Preview/TempleActivePreview.cs) | Ensaio isolado da rota diplomática de 5 escolhas de Cameron (identificado no arquivo `maps/arcaves.map` pelo offset físico do registro do objeto `425776`, com script `scripts/actemvil.int` cujas opções correspondem aos offsets de instrução do bytecode `11698`, `11956`, `12650`, `14192` e `14328`, para Int $\ge$ 4 e Speech tagueada) em janela de texto (sem talking head). Possui persistência própria em JSON isolado (`temple-speech.json`, `temple-departure.json`). Não é um motor de diálogo genérico. Sua integração à sessão viva ocorre exclusivamente via ação explícita no HUD (`ApplyRequested` $\to$ `CommitTempleSpeech`), que projeta Cameron no mapa 3 e permite avançar seus passos via HUD ativo, sendo persistido em `WorldCheckpoint` (schema v6+). |
| **Transição para Arroyo e Recompensas** | **Integrado à Sessão e Validado em Teste Headless** | [TempleVillageRoute.cs](../src/Runtime/TempleVillageRoute.cs), [ArroyoRewardProfile.cs](../src/Runtime/ArroyoRewardProfile.cs), [TempleVillageChecks.cs](../src/Preview/TempleVillageChecks.cs) | Rota até a porta 100, travessia com tecla `Enter` para o mapa 4, concessão de 600 XP (acumulando 625 XP totais com o arrombamento anterior) e persistência de Karma 15 e Reputação 65. *Comprovado via método assíncrono headless e atalhos de HUD, mas não constitui campanha contínua de gameplay final.* |
| **Assets 3D de Natureza** | **Exercitável no Protótipo Visual** | [NatureModels.cs](../src/Preview/NatureModels.cs), [NaturePicking.cs](../src/Preview/NaturePicking.cs), [VisualPreferences.cs](../src/Runtime/VisualPreferences.cs) | Modelos GLB otimizados de rochas e árvores queimadas substituem os marcadores visuais primitivos na cena. Picking 3D preciso por triângulos na malha e persistência de preferências visuais v2 com fallback gracioso. |
| **Interação com a Anciã de Arroyo** | **Parcial (Apenas Ensaio Desacoplado)** | [ArroyoElderAudit.cs](../tools/TempleInspector/ArroyoElderAudit.cs), [ArroyoElderStateProfile.cs](../src/Runtime/ArroyoElderStateProfile.cs), [ArroyoElderRepeatAudit.cs](../tools/TempleInspector/ArroyoElderRepeatAudit.cs) | Auditoria de abertura original, leitura de atributos PRO (Carisma 5), reação neutra inicial (32) e ensaio de primeiro diálogo concluídos. **O diálogo ativo e a ligação à partida viva continuam estritamente desabilitados** (`RuntimeDialogueEnabled=false`). |
| **Apresentação Visual / HUD** | **Protótipo de Testes** | [TemplePreview.cs](../src/Preview/TemplePreview.cs), [PreviewHud.cs](../src/Preview/PreviewHud.cs) | Paredes e pisos representados por caixas/primitivas provisórias com corte de oclusão. Câmera isométrica ortográfica funcional, mas sem iluminação final ou shaders de terreno. |
| **Demais Categorias de Assets 3D** | **Parcial (Importados, Não Mapeados na Cena)** | Pastas em `Assets/Architecture`, `Assets/Containers`, `Assets/Furniture`, `Assets/Props` | 15 modelos GLB otimizados (WebP-92 @ 1024px) importados com `.import` válidos, mas ainda não mapeados na cena para substituir os objetos originais (somente `Nature` está mapeada). |
| **Combate por Turnos e Ações** | **Integrado à Sessão, Persistido e Exercitável no Protótipo** (branch `agent/claude-work`) | [CombatSession.cs](../src/Runtime/Combat/CombatSession.cs), [WorldCombat.cs](../src/Runtime/WorldCombat.cs), [WorldTempleDuel.cs](../src/Runtime/WorldTempleDuel.cs), [CombatViewAdapter.cs](../src/Preview/CombatViewAdapter.cs) | Encontros com formigas e radscorpions dos PRO originais: AP, chance de acerto e dano do CE (DT/DR), IA provisória (aproxima e ataca), Healing Powder em combate (4 AP) e o desafio desarmado de Cameron com rendição abaixo de metade do HP. Resultado salvo desde o checkpoint v10. Sem mira, fuga por AI packet nem veneno. Críticos não entram, como no original durante o primeiro dia de jogo ([skill-spending-2026-10-31.md](skill-spending-2026-10-31.md)). |
| **Progressão e Ficha do Personagem** | **Integrado à Sessão, Persistido e Exercitável no Protótipo** (branch `agent/claude-work`) | [PlayerCharacter.cs](../src/Runtime/PlayerCharacter.cs), [CharacterFile.cs](../src/Import/CharacterFile.cs), [CharacterProgression.cs](../src/Runtime/CharacterProgression.cs), [WorldSkills.cs](../src/Runtime/WorldSkills.cs) | Narg, Mingan e Chitsa lidos dos GCD originais; criação com 40 pontos, até 2 traits e 3 tags; níveis por XP com HP e pontos de perícia por nível; gasto dos pontos com os custos do CE (checkpoint v13). Sem perks e sem os traits que agem fora da ficha. |
| **Build Android / Mobile** | **Não Verificado** | [Fallout Remake.csproj](../Fallout%20Remake.csproj#L4) | Existe declaração condicional de `net9.0` para Android, mas sem automação ou testes que comprovem a execução mobile. |

---

## 4. Distinção Crucial: O que é o Protótipo Atual

O projeto atual **não é uma fatia vertical jogável de RPG comercial**. Trata-se de uma **cena de inspeção interativa e bancada de validação de regras** (`TemplePreview`):

1. **Sobre o Percurso Templo $\to$ Arroyo (625 XP acumulados):**
   - **O que está comprovado:** No protótipo de teste headless ([TempleVillageChecks.cs](../src/Preview/TempleVillageChecks.cs)), a máquina de estados `WorldSession` é exercitada através de comandos simulados (botões de rota no HUD, tecla `U` para porta 100, tecla `Enter` na saída). O sistema transporta o personagem para o mapa 4 (`arvillag.map`), credita 600 XP uma única vez e salva o checkpoint `v9` com Karma 15 e Reputação 65, verificando inclusive o retorno ao Templo sem duplicação de XP.
   - **O que NÃO existe:** Não há transição cinemática, não há salvamento de quests com interface de Pip-Boy, não há diálogos com habitantes da aldeia e a caminhada ocorre através de teleporte de hexes ou interpolação linear sem pontos de ação ou perigos de combate.
2. **Sobre Ensaios Desacoplados vs. Sessão:**
   - **Ensaio Estritamente Desacoplado:** Classes como [ArroyoElderStateProfile](../src/Runtime/ArroyoElderStateProfile.cs) rodam apenas em cópias isoladas de memória para auditar se os branchings de diálogo conferem com o disassembly do arquivo `.int` da Anciã. Elas **não afetam** o estado do mundo nem concedem experiência em tempo de jogo (`RuntimeDialogueEnabled = false`).
   - **Ensaio Desacoplado com Ponte de Integração:** O diálogo diplomático de Cameron ([TempleSpeechDraft](../src/Runtime/TempleSpeechDraft.cs)) e a simulação de sua saída ([TempleNpcDeparture](../src/Runtime/TempleNpcDeparture.cs)) funcionam como ensaios desacoplados com persistência própria em arquivos isolados da campanha (`.local/checkpoints/temple-speech.json` e `temple-departure.json`). O ensaio **não altera** a partida automaticamente. A integração à sessão viva ocorre apenas quando o ensaio aceito é promovido via ação explícita no HUD (`ApplyRequested` em [TempleSpeechPreview.cs](../src/Preview/TempleSpeechPreview.cs#L81-L95)), que executa `WorldSession.CommitTempleSpeech` ([WorldTempleSpeech.cs](../src/Runtime/WorldTempleSpeech.cs#L103-L114)). A partir daí, o progresso do NPC passa a ser projetado no mundo (`EffectiveObject`), controlado pelas ações ativas do HUD ([TempleActivePreview.cs](../src/Preview/TempleActivePreview.cs)) e salvo no checkpoint oficial da campanha ([WorldCheckpoint.cs](../src/Runtime/WorldCheckpoint.cs), schema v6+).

---

## 5. Suítes de Teste e Limites Reais de Comprovação

### A. Testes Sintéticos Unitários (`tools/TempleInspector`)
* **Comando:** `dotnet run --project "tools/TempleInspector/TempleInspector.csproj" -- --self-test`
* **Tempo:** Medido em ~1,4 segundo (portável, sem Godot, sem arquivos externos).
* **O que comprova:** 335 grupos de teste executados sobre fixtures sintéticas em memória (`Fixture`), cobrindo parsers binários, validação estrutural round-trip e detecção de corrupção via `MapLayoutWriter.Verify` ([StructuralTests.cs](../tools/TempleInspector/StructuralTests.cs#L22,L149)), regras euclidianas do grid hex, enquadramento matemático de câmera, conservação de itens e migração de checkpoints v1 a v9.
* **O que NÃO comprova:** Não acessa os arquivos originais da instalação clássica (portanto, a suíte sintética isolada **não comprova** por si só a identidade byte a byte dos mapas reais em disco), não executa nós do Godot, não testa áudio, física ou compatibilidade de shaders.

### B. Relatórios de Conformidade de Dados
* **Comando:** `dotnet run --project tools/TempleInspector/TempleInspector.csproj -- --data-dir "<fallout2>" --<nome>-report`
* **Tempo Estimado:** ~0,5s por relatório.
* **O que comprova:** Conformidade estrita de offsets, tabelas e estruturas em 14 relatórios JSON contra os arquivos originais da instalação clássica. **É nesta etapa** que `MapLayoutWriter.Verify(map)` ([MapLayoutWriter.cs#L83-L87](../src/Import/MapLayoutWriter.cs#L83-L87)) lê os arquivos reais (`maps/arcaves.map` e `maps/arvillag.map`) da instalação, reconstrói os binários a partir dos campos interpretados e executa a comparação explícita byte a byte (`SequenceEqual`) contra os bytes brutos lidos da instalação, emitindo `"OriginalReconstruction": "verified"` (validado no pipeline em [Validate-Temple.ps1#L54-L57](../tools/Validate-Temple.ps1#L54-L57)).

### C. Validação Integrada Headless (`tools/Validate-Temple.ps1`)
* **Comando:** `powershell -File tools/Validate-Temple.ps1 -GodotExe "<godot-console.exe>" -DataDirectory "<fallout2>"`
* **Tempo:** Medido em ~88 segundos na última execução automatizada registrada.
* **O que comprova:** Compilação C#, 335 testes sintéticos, 14 relatórios JSON e execução in-engine em modo `--headless` dos testes assíncronos (`--nature-test` e `--preview-test`). O validador confirma saída sem exceções não tratadas e ausência de mensagens com severidade ERROR ou WARNING registradas no console do Godot.
* **O que NÃO comprova (Limitações Críticas):**
  - **NÃO comprova fidelidade visual ou ausência de falhas gráficas:** Por rodar em modo `--headless`, nenhum pipeline de rasterização ou apresentação na tela é testado.
  - **NÃO comprova ausência de vazamentos de memória:** Nenhum profiler de heap ou análise estrita de alocação de longo prazo é executado; apenas confirma que o processo encerra sem quebra de execução.

---

## 6. Próximo Marco Oficial

Conforme registrado na conclusão da última bateria documental ([docs/arroyo-elder-batch-2026-10-21.md](arroyo-elder-batch-2026-10-21.md#L53-L56)) e verificado no código:

* **Próximo Passo:** Estender o ensaio separado de estado para o **Node004** da Anciã de Arroyo no contexto neutro (com Vault 13 desconhecida no mapa e `L7 = 0`), auditando guardas e ramificações de uma segunda conversa, antes de conectar ativamente o diálogo ao savegame e ao loop de jogo ativo.
