# FalloutRemake — Decisões Arquiteturais e Registro de Decisões de Design (ADR)

> **Status:** Registro Canônico de Decisões Arquiteturais e Funcionais  
> **Data de Elaboração:** 28 de Outubro de 2026  
> **Autor da Análise:** Gemini Flash 3.8 (Agente Solo)  
> **Base de Código:** Commit `a66f31c`  
> **Documento Principal:** [gameplay-combat-roadmap.md](file:///c:/Dev/FalloutRemake/docs/roadmap/gameplay-combat-roadmap.md)

---

## 1. Decisões Arquiteturais Consolidadas (Confirmadas no Código)

As decisões abaixo já estão implementadas, testadas e homologadas na base de código, constituindo as fundações invioláveis do projeto:

### ADR-01: Desacoplamento Estrito entre Domínio C# Puro (.NET 8) e Godot 4
- **Decisão:** Os subsistemas de regras de negócio, decodificação de formatos, matemática espacial e persistência residem em `src/Import/`, `src/Geometry/` e `src/Runtime/` sem nenhuma dependência do motor Godot.
- **Evidência:** O projeto de console `tools/TempleInspector/TempleInspector.csproj` referencia diretamente esses módulos e executa 337 grupos de testes sintéticos em ~1,4 s sem instanciar a engine gráfica.
- **Impacto no Combate:** O núcleo de combate tático (`src/Runtime/Combat/`) deve seguir rigorosamente esse mesmo padrão, permitindo que 100% da lógica matemática seja testada no console.

### ADR-02: Topologia Espacial de 40.000 Hexágonos
- **Decisão:** O espaço lógico do jogo utiliza um grid hexagonal de $200 \times 200$ células com paridade em colunas alternadas (`FalloutGrid.cs`), idêntico à convenção binária de Fallout 2.
- **Evidência:** `FalloutGrid.HexCenter`, `FalloutGrid.Distance` e `HexNavigation.ReachableFrom` conferem determinismo absoluto com o algoritmo de `tileDistanceBetween` do Community Engine.

### ADR-03: Persistência Atômica Multi-Mapa (Checkpoints v1–v9)
- **Decisão:** Checkpoints são salvos em arquivos JSON estritos ($\le 1 \text{ MB}$) com substituição atômica de arquivos no disco (`AtomicFileReplacement.cs`), garantindo que falhas de gravação nunca corrompam o save anterior.

---

## 2. Registro de Decisões Pendentes de Design (PDR)

As decisões a seguir tratam de regras de combate, modernizações e políticas de jogo que impactam o balanceamento ou a arquitetura. Cada decisão detalha alternativas, recomendação e se exige aprovação explícita do usuário.

---

### [DEC-001] Fórmula Definitiva de Chance de Acerto (To Hit) e Limites de Clamping
- **Pergunta Concreta:** Devemos manter os limites clássicos estritos de 5% (mínimo) e 95% (máximo), ou adotar uma escala dinâmica modernizada (onde tiros à queima-roupa podem atingir 99% ou 100%)?
- **Alternativas:**
  - *Alternativa A (Clássica):* Clamping rígido entre 5% e 95%. Rolagens de 96–100 no d100 são sempre erros, independentemente da perícia.
  - *Alternativa B (Moderna / XCOM):* Clamping entre 0% e 100%. Tiros a 1 hex de distância com arma de contato podem atingir 100% garantido.
  - *Alternativa C (Híbrida):* Clamping 5% a 95% para armas de fogo; 5% a 99% para combate corpo a corpo desarmado/lança a curta distância.
- **Recomendação:** **Alternativa A.** Preserva a identidade estatística do Fallout clássico e a imprevisibilidade autêntica do combate por turnos.
- **Consequências Técnicas:** Código simples com `Math.Clamp(toHit, 5, 95)`.
- **Consequências de Balanceamento:** Evita ataques com 100% de certeza absoluta, incentivando cautela e planos de contingência.
- **Impacto no Paralelismo:** Nenhum. Fica isolado dentro de `CombatResolver.cs`.
- **Exige Aprovação do Usuário:** **Sim.**

---

### [DEC-002] Mecânica e Penalidades de Cobertura Posicional
- **Pergunta Concreta:** A cobertura deve atuar como uma penalidade direta na Chance de Acerto do atacante, como um bônus de Classe de Armadura (AC) do alvo, ou ambos?
- **Alternativas:**
  - *Alternativa A (Moderna Pura):* Meia cobertura aplica $-20\%$ de acerto; Cobertura total aplica $-40\%$ de acerto. Sem alterar AC.
  - *Alternativa B (Bônus de AC):* Meia cobertura concede $+15 \text{ AC}$; Cobertura total concede $+30 \text{ AC}$.
  - *Alternativa C (Composta):* Meia cobertura aplica $-20\%$ acerto e $+10 \text{ AC}$; Cobertura total quebra linha de visão para ataques diretos, salvo se o alvo estiver espiando pela quina (-40% acerto).
- **Recomendação:** **Alternativa C.** Oferece a resposta mais rica: reduz a chance de acerto e melhora a margem de defesa do alvo.
- **Consequências Técnicas:** Requer cálculo geométrico de intersecção de hexes em `src/Geometry/CombatCover.cs`.
- **Consequências de Balanceamento:** Incentiva fortemente os combatentes a buscarem obstáculos físicos em vez de lutarem em campo aberto.
- **Exige Aprovação do Usuário:** **Sim.**

---

### [DEC-003] Ordem dos Modificadores de Dano e Multiplicador Crítico
- **Pergunta Concreta:** O multiplicador de dano crítico (ex.: 2.0x, 3.0x) deve incidir sobre o dano bruto da arma antes do DT, ou sobre o dano líquido após o DT da armadura?
- **Alternativas:**
  - *Alternativa A (Clássica Fallout 2 CE):* $\text{Dano Bruto} \times \text{Multiplicador Crítico} \to \text{Subtrai DT} \to \text{Aplica DR \%}$.
  - *Alternativa B (Dano Líquido):* $(\text{Dano Bruto} - \text{DT}) \times \text{Multiplicador Crítico} \to \text{Aplica DR \%}$.
  - *Alternativa C (Penetração Total):* Críticos ignoram 100% do DT da armadura e multiplicam o dano bruto.
- **Recomendação:** **Alternativa A.** No Fallout 2 original, golpes críticos multiplicam o dano antes da armadura, permitindo que acertos em pontos fracos superem armaduras pesadas naturalmente.
- **Consequências Técnicas:** Alinha-se diretamente com o algoritmo de `combat_damage` do CE.
- **Exige Aprovação do Usuário:** Não (reproduz a regra confirmada de Fallout 2).

---

### [DEC-004] Hipótese de Balanceamento: Piso de Dano Mínimo Efetivo
- **Pergunta Concreta:** Devemos implementar um piso mínimo de dano efetivo (10% do dano bruto ou 1 HP) em ataques bem-sucedidos para evitar anulação total contra DT alto?
- **Alternativas:**
  - *Alternativa A (Clássica):* Se $\text{Dano Bruto} \le \text{DT}$, o dano é zero (`0 HP`).
  - *Alternativa B (Piso de 10%):* Dano efetivo mínimo é $\max(1, \lfloor\text{Dano Bruto} \times 0.10\rfloor)$, desde que o ataque acerte.
  - *Alternativa C (Piso Fixo de 1 HP):* Todo ataque que acerta causa pelo menos 1 HP de dano por impacto cinético.
- **Recomendação:** **Alternativa B.** Torna armas leves minimamente úteis contra inimigos resistentes sem anular o valor de armaduras avançadas (Power Armor).
- **Consequências Técnicas:** Introduz condicional em `DamageCalculator.cs`.
- **Consequências de Balanceamento:** Evita situações em que o jogador fica completamente bloqueado por falta de dano contra criaturas blindadas no início do jogo.
- **Exige Aprovação do Usuário:** **Sim (Decisão de Balanceamento Crítica).**

---

### [DEC-005] Penalidades e Tabelas de Mira por Região Corporal
- **Pergunta Concreta:** As penalidades de acerto por parte do corpo devem seguir estritamente as tabelas originais de Fallout 2 ou sofrer ajustes para maior clareza visual?
- **Alternativas:**
  - *Alternativa A (Clássica Estrita):* Olhos -60%, Cabeça -40%, Virilha -30%, Braços -30%, Pernas -20%, Tronco 0%. Custo adicional fixo de +1 AP.
  - *Alternativa B (Ajustada):* Olhos -50%, Cabeça -35%, Braços -25%, Pernas -20%, Virilha -25%.
- **Recomendação:** **Alternativa A.** As tabelas clássicas são consagradas e amplamente testadas pela comunidade.
- **Consequências Técnicas:** Implementação direta em `TargetedShots.cs`.
- **Exige Aprovação do Usuário:** Não (preserva regras originais).

---

### [DEC-006] Resolução de Concorrência de Overwatch
- **Pergunta Concreta:** Quando múltiplos combatentes estão em Overwatch vigiando o mesmo hexágono e um inimigo entra no alcance, qual é a ordem de resolução dos disparos de reação?
- **Alternativas:**
  - *Alternativa A (Iniciativa / Sequence):* Os combatentes em vigilância reagem na ordem de sua estatística de `Sequence`.
  - *Alternativa B (Distância):* O combatente mais próximo do alvo reage primeiro.
  - *Alternativa C (Simultânea):* Todos os disparos elegíveis ocorrem simultaneamente no mesmo instante lógico.
- **Recomendação:** **Alternativa A.** Respeita a identidade do SPECIAL, onde maior Percepção e Iniciativa conferem reflexos mais rápidos de combate.
- **Consequências Técnicas:** Fila ordenada de eventos de reação em `OverwatchSession.cs`.
- **Exige Aprovação do Usuário:** Não (decisão técnica natural).

---

### [DEC-007] Dispersão de Rajada (Burst Fire) e Fogo Amigo
- **Pergunta Concreta:** O cone de rajada deve utilizar dispersão por linha de visão discreta de projéteis ou distribuição estatística por setores do cone?
- **Alternativas:**
  - *Alternativa A (Trajetória Discreta por Projétil):* Cada bala do pente é traçada como um raio físico independente com leve desvio angular.
  - *Alternativa B (Setores do Cone):* O cone é dividido em 3 faixas (Centro 60% das balas, Esquerda 20%, Direita 20%). Alvos em cada faixa recebem rolagem proporcional.
- **Recomendação:** **Alternativa A.** Produz resultados táteis autênticos e permite que coberturas no cenário bloqueiem parte dos tiros da rajada naturalmente.
- **Consequências Técnicas:** Requer raycasts múltiplos em `BurstFireResolver.cs`.
- **Exige Aprovação do Usuário:** Não.

---

### [DEC-008] Tratamento de Fogo Amigo para o Jogador e Companions
- **Pergunta Concreta:** O sistema deve impedir disparos que atinjam aliados ou apenas emitir alertas visuais de perigo?
- **Alternativas:**
  - *Alternativa A (Aviso e Confirmação):* O jogador pode disparar se desejar, mas a UI destaca os aliados em vermelho com uma caixa de confirmação ("Aliado em Perigo"). Para a IA autônoma, o fogo amigo é estritamente proibido.
  - *Alternativa B (Bloqueio Absoluto):* Disparos com probabilidade $> 0\%$ de atingir aliados são desabilitados.
  - *Alternativa C (Clássica sem Aviso):* O disparo ocorre sem nenhum aviso especial, atingindo aliados se estiverem na linha de tiro.
- **Recomendação:** **Alternativa A.** Preserva a liberdade do jogador em situações desesperadas, mas impede acidentes desastrosos de IA.
- **Exige Aprovação do Usuário:** **Sim.**

---

### [DEC-009] Persistência do Gerador Pseudo-Aleatório (RNG) e Prevenção de Save-Scumming
- **Pergunta Concreta:** Ao salvar o combate, devemos serializar o estado interno exato do RNG para evitar save-scumming, ou reiniciar a semente no carregamento?
- **Alternativas:**
  - *Alternativa A (Determinismo Estrito):* O estado do RNG é serializado no save. Recarregar o jogo e repetir a mesma ação resultará no mesmo acerto/erro.
  - *Alternativa B (Re-seeding no Load):* O jogo gera uma nova semente ao carregar, permitindo rolagens diferentes.
  - *Alternativa C (Fila de Dados Pré-Rolados):* As próximas 10 rolagens são armazenadas no save.
- **Recomendação:** **Alternativa A.** Garante reproducibilidade em testes, depuração de bugs e integridade da simulação tática.
- **Consequências Técnicas:** `CombatRngSession` implementa serialização de semente e contador de passos.
- **Exige Aprovação do Usuário:** **Sim.**

---

### [DEC-010] Condições de Invalidação do Desfazer Movimento (Movement Undo)
- **Pergunta Concreta:** Quais eventos devem invalidar instantaneamente o botão de desfazer caminhada?
- **Alternativas:**
  - *Alternativa A (Estrita):* Qualquer ataque, uso de item, dano sofrido, armadilha pisada ou novo inimigo revelado na névoa de guerra cancela o undo.
  - *Alternativa B (Permissiva):* Apenas ataques ou uso de itens cancelam o undo; exploração de névoa é reversível.
- **Recomendação:** **Alternativa A.** O jogador não pode usar o "desfazer" para revelar posições inimigas ou testar se há armadilhas e depois recuar sem custo.
- **Consequências Técnicas:** `MovementUndoStack` monitora eventos de descoberta e combate.
- **Exige Aprovação do Usuário:** Não.

---

### [DEC-011] Autonomia dos Companions: Controle Direto vs IA Autônoma
- **Pergunta Concreta:** O modo padrão de controle de companheiros deve ser manual (Full Party Control) ou IA autônoma?
- **Alternativas:**
  - *Alternativa A (Controle Manual Padrão):* Jogador controla diretamente cada membro do grupo; IA é ativada seletivamente por opção.
  - *Alternativa B (IA Padrão com Ordens):* Companions operam por IA clássica, recebendo ordens gerais de postura.
- **Recomendação:** **Alternativa A.** Alinha-se diretamente com o objetivo de produto solicitado e com referências aclamadas (Wasteland 3, sfall).
- **Exige Aprovação do Usuário:** **Sim (Confirmada pelo escopo do roadmap).**

---

### [DEC-012] Omissão de Animações Fora do Campo de Visão (Off-Screen Skip)
- **Pergunta Concreta:** A simulação de inimigos distantes fora da tela deve ser puramente matemática em C# sem gerar nós ou malhas 3D?
- **Alternativas:**
  - *Alternativa A:* Sim. Se o inimigo não for visível pelo jogador ou aliados, seu turno é resolvido instantaneamente em C# puro.
  - *Alternativa B:* Não. Todos os inimigos se movem na mesma velocidade física na cena 3D.
- **Recomendação:** **Alternativa A.** Elimina o tempo morto e otimiza a performance em mapas grandes como Templo e Den.
- **Consequências Técnicas:** `CombatViewAdapter` verifica visibilidade antes de despachar animações visuais.
- **Exige Aprovação do Usuário:** Não.

---

### [DEC-013] Modelo de Navegação com Gamepad
- **Pergunta Concreta:** O controle deve mover um cursor virtual livre ou navegar de hex em hex através dos 6 vizinhos do grid?
- **Alternativas:**
  - *Alternativa A (Snap Hexagonal Tático):* O stick analógico e D-Pad saltam o cursor diretamente entre os hexágonos vizinhos.
  - *Alternativa B (Cursor Virtual Emulado):* O stick move uma seta de mouse virtual livre pela tela.
- **Recomendação:** **Alternativa A.** Proporciona ergonomia de console de ponta, evitando imprecisões no alinhamento de cliques.
- **Consequências Técnicas:** Algoritmo de vizinhança em `GamepadTacticalInput.cs`.
- **Exige Aprovação do Usuário:** Não.

---

### [DEC-014] Evolução de Checkpoint para Schema v10
- **Pergunta Concreta:** Como persistir o estado de diálogo da Anciã e do combate sem quebrar compatibilidade com saves v1–v9?
- **Alternativas:**
  - *Alternativa A (Extensão Compatível v10):* Incrementa a versão para 10, adiciona o campo opcional `Elder` e `CombatSession` em `WorldCheckpoint`, e migra saves anteriores preenchendo nulos.
  - *Alternativa B (Novo Arquivo Separado):* Salva combate e diálogos em arquivos JSON à parte.
- **Recomendação:** **Alternativa A.** Preserva a arquitetura consolidada de persistência única e atômica.
- **Exige Aprovação do Usuário:** Não.

---

### [DEC-015] Contrato Desacoplado `ICombatSession`
- **Pergunta Concreta:** Onde a sessão de combate deve residir no grafo de classes em relação a `WorldSession`?
- **Alternativas:**
  - *Alternativa A (Propriedade Opcional de WorldSession):* `WorldSession.Combat` armazena a instância ativa de `ICombatSession` quando em combate, ou nulo em exploração normal.
  - *Alternativa B (Monólito Integrado):* Mesclar métodos de combate diretamente dentro de `WorldSession.cs`.
- **Recomendação:** **Alternativa A.** Mantém `WorldSession` limpo e focado no ciclo de vida de mapas e contêineres, delegando o combate a um componente especializado.
- **Exige Aprovação do Usuário:** Não.
