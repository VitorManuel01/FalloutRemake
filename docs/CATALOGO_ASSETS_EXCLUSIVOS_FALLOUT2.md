# Catálogo de Assets Exclusivos de Fallout 2 (Diretrizes para Reconstrução com Claude)

> **Finalidade:** Documentação técnica, narrativa e visual dos recursos icônicos e exclusivos do Fallout 2 original.  
> **Diretriz de Escopo:** **NÃO iniciar modelagem prematura nesta fase.** Registrar com precisão suas propriedades, caminhos de origem nos DATs, protótipos lógicos, coordenadas, silhueta e proporções para que o agente Claude possa realizar a modelagem 3D artística em etapas futuras.

---

## 1. Princípios de Fidelidade Estética e Arquitetura

O prólogo de Fallout 2 em Arroyo possui uma identidade visual única no universo Fallout: uma fusão de **arquitetura tribal pré-colombiana (asteca/maia/olmeca)** com os resquícios tecnológicos do herói do primeiro Fallout (o *Vault Dweller*), ambientada em um platô montanhoso árido de clima desértico.

A direção visual do remake busca uma apresentação isométrica 3D moderna inspirada em *Wasteland 3*, preservando a volumetria e proporções clássicas sem anacronismos estilísticos.

---

## 2. Catálogo Estruturado de Estruturas e Objetos Exclusivos

### A. A Cabeça Colossal do Vault Dweller (Fachada do Templo dos Testes)
- **Localização:** Fachada externa do Templo dos Testes (`maps/artemple.map`, Map ID 126).
- **Identificadores Clássicos:**
  - PIDs dos Módulos do Templo: `0x0200054F` a `0x02000554`, `0x02000558`, `0x02000559`.
  - FIDs de Arte: `0x0200055D` a `0x02000567` (`art/scenery/temple*.frm`).
- **Contexto Narrativo:** Uma gigantesca cabeça esculpida na rocha da montanha em reverência tribal ao Vault Dweller de Fallout 1, usando o clássico macacão Vault 13 estilizado com formas geométricas de pedra.
- **Silhueta e Proporções:** Estilo monumental olmeca, proporção aproximadamente 2:1 em relação à altura de um personagem humano, com feições austeras esculpidas em arenito/granito envelhecido e fendas de erosão.
- **Aspectos a Preservar na Recriação (Claude):**
  - Textura de pedra esculpida manualmente com ranhuras secas e musgo incrustado nas fendas.
  - O portal de entrada do templo escavado logo abaixo ou na base da escultura.
  - Integração contínua com a parede de penhasco do cânion (`CanyonCliffWall`).

---

### B. As Portas Pesadas de Pedra Deslizante do Templo
- **Localização:** Interior do Templo dos Testes (`maps/arcaves.map`, Map ID 3, Elevações 0, 1 e 2).
- **Identificadores Clássicos:**
  - PID: `0x020003A6` (Door) e `0x0200055C` (Door Track / Trilho de Pedra).
  - FID: `0x020003B1` e `0x0200056A` (`art/scenery/tibdor01.frm` e `art/scenery/tibtrk01.frm`).
  - Script associado: `scripts/aitemdor.int`.
- **Funcionamento:** Lajes monolíticas maciças de pedra que deslizam lateralmente sobre um trilho escavado no piso de rocha. A 2ª porta é trancada por uma fechadura rústica (Lockpick, 25 XP), e a porta final (100) exige a Chave do Templo ou permissão de Cameron.
- **Aspectos a Preservar na Recriação (Claude):**
  - Entalhes circulares e frisos geométricos tribais na face visível da laje.
  - Trilho entalhado no piso com desgaste mecânico por atrito e poeira acumulada.
  - Animação de abertura por translação suave no eixo horizontal.

---

### C. A Grande Ponte Suspensa de Arroyo
- **Localização:** Arroyo Bridge (`maps/arbridge.map`, Map ID 5).
- **Identificadores Clássicos:**
  - PIDs: Módulos de passadiço de tábuas e amarrações de corda.
  - FID de Arte: `art/scenery/arbridg*.frm`.
  - Guarda: Mynoc (PID `0x0100000F`, `scripts/acmynoc.int`).
- **Contexto Narrativo:** Uma ponte colossal estaiada que cruza o grande desfiladeiro rochoso de Arroyo, ligando o vilarejo tribal ao vasto deserto da Califórnia.
- **Silhueta e Proporções:** Longa extensão suspensa por cabos de cipó e cordas grossas trançadas, com pranchas de madeira rústica amarradas individualmente, apresentando arqueamento central e leve balanço.
- **Aspectos a Preservar na Recriação (Claude):**
  - Variação natural nas tábuas (larguras irregulares, pranchas rachadas).
  - Cordame de sustentação fixado em postes de troncos cravados na rocha em ambas as margens.
  - Sensação de vertigem sobre o abismo do cânion.

---

### D. Tendas Tribais e Curral da Aldeia de Arroyo
- **Localização:** Arroyo Village (`maps/arvillag.map`, Map ID 4).
- **Identificadores Clássicos:**
  - FIDs de Arte: `art/scenery/tents*.frm`, `art/scenery/arcurral*.frm`.
- **Estruturas Principais:**
  - **Tenda da Anciã:** Tenda cônica ampla feita de peles curtidas de brahmin costuradas com tendões sobre vigas de madeira inclinadas.
  - **Tenda de Hakunin:** Tenda cerimonial com secadores de ervas medicinais, raízes de xander penduradas e braseiros aromáticos.
  - **Curral de Brahmin:** Cercado rústico feito de troncos amarrados e estacas pontiagudas guardando 4 animais.
- **Aspectos a Preservar na Recriação (Claude):**
  - Textura translúcida e opaca de couro seco curtido, com costuras grossas de fibra e remendos.
  - Estacas de madeira amarradas com cordas nas junções.

---

### E. O Poço Comunitário da Aldeia (Arroyo Well)
- **Localização:** Centro da Aldeia (`maps/arvillag.map`, Map ID 4).
- **Identificadores Clássicos:**
  - PID: `0x02000021` (Well).
  - FID: `art/scenery/well01.frm`.
- **Contexto:** Poço circular construído em pedra argamassada com cobertura de palha/madeira e corda enrolada em carretel, central para a vida da tribo e ponto de encontro dos aldeões.
- **Aspectos a Preservar (Claude):**
  - Alvenaria rústica de pedras arredondadas de rio.
  - Madeira desgastada pela umidade e balde de couro ou madeira.

---

## 3. Catálogo de Criaturas Clássicas Exclusivas (Rigged & Animadas)

| Criatura | Instâncias na Fatia 1 | PID / FID | Características Morfológicas e Comportamentais | Animações Críticas Requeridas |
| :--- | :---: | :---: | :--- | :--- |
| **Giant Ant (Formiga Gigante)** | 12 (Templo, Elevações 0 e 1) | PID `0x0100017E`<br/>FID `0x01000061` | Formiga mutante de carapaça escura brilhante, ~1,2m de comprimento. Mandíbulas pontiagudas em pinça, 6 patas articuladas e antenas táteis ativas. | Idle (antenas mexendo), Walk rápido, Attack (mordida frontal), Hurt (recuo), Death (pernas encolhendo). |
| **Small Radscorpion (Escorpião Radioativo)** | 9 (Templo, Elevações 0 e 1) | PID `0x01000005`<br/>FID `0x0100003B` | Escorpião imperador mutante de carapaça marrom-avermelhada, pinças robustas e cauda com ferrão venenoso em arco sobre as costas. | Idle (pinças erguidas), Walk, Attack (estocada de ferrão), Attack (pinça), Death (desabamento de costas). |
| **Brahmin de Duas Cabeças** | 4 (Aldeia, Map 4) | PID `0x0100000A`<br/>FID `0x0100000F` | Bovino mutante corpulento com duas cabeças assimétricas, úbere e respiração pesada. Movimentos lentos e pacíficos. | Idle (mastigação, balanço de cauda), Walk lento, Lie Down. |
| **Spore Plant (Planta de Esporos)** | 2 (Jardim de Hakunin) | PID `0x01000010`<br/>FID `0x01000041` | Planta carnívora rasteira com bulbo central dentado, caules tentaculares e capacidade de disparar espinhos de esporos à distância. | Idle (ondulação de tentáculos), Shoot (disparo de espinho), Bite (abocanhar), Death (murchar). |

---

## 4. Catálogo de NPCs Tribais Humanos (Rigged & Animados)

1. **Cameron / Dumar (Guerreiro Guardião do Templo):**
   - Localização: Hex `24302`, Elevação 2 de `arcaves.map` (Porta 100).
   - PID: `0x01000112`, FID: `0x01000040`. Script: `scripts/actemvil.int`.
   - Visual: Guerreiro tribal de porte atlético, tanga de couro cru, pintura corporal branca/ocre de argila, cabelos compridos amarrados e colar de presas.
   - Postura: Guarda de combate desarmado em artes marciais tribais e fala cerimonial solene.
2. **A Anciã de Arroyo (The Elder):**
   - Localização: Tenda comunal em `maps/arvillag.map`.
   - PID: `0x0100000D`, FID: `0x0100001F`. Script: `scripts/ahelder.int`.
   - Visual: Matriarca idosa, vestes longas de tecido rústico e peles, colar de dentes de geckos e cajado cerimonial entalhado.
3. **Hakunin (Xamã de Arroyo):**
   - Localização: Tenda e jardim em `maps/arvillag.map` e `argarden.map`.
   - PID: `0x0100000E`, FID: `0x0100001F`. Script: `scripts/ahhakun.int`.
   - Visual: Curandeiro místico, túnica rústica manchada com pigmentos vegetais de Broc e Xander, bolsas de ervas na cintura e adereços de ossos.
4. **Aldeões e Guerreiros Genéricos:**
   - 13 civis em Arroyo (homens e mulheres, PIDs `0x01000003` e `0x01000004`).
   - Visual: Camponeses com tangas de couro e túnicas simples, trabalhando no curral, perto do poço ou conversando.

---

## 5. Diretrizes para Retomada da Modelagem (Claude)

Quando o desenvolvimento avançar para a fase de criação de novos modelos 3D, Claude deverá:
1. Utilizar os identificadores originais (PIDs/FIDs) e as referências dos sprites `.frm` extraídos de `master.dat` como gabarito volumétrico e ortogonal.
2. Esculpir em Blender com malha otimizada para isometria 3D (mid-poly com bake de normal maps).
3. Exportar no formato padrão da pipeline: glTF 2.0 binário (`.glb`) com materiais PBR (Albedo WebP, Normal OpenGL `+Y`, ORM `Roughness/Metallic`).
4. Seguir as regras de preservação de rigs humanoides para compatibilidade de animação entre os personagens tribais.
