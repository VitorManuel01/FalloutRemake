# Criação de personagem e os três personagens prontos

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

O jogo começa com a escolha do personagem:

- Narg, Mingan ou Chitsa, lidos dos arquivos originais;
- um personagem criado com as regras do editor do Fallout 2;
- o perfil de teste neutro dos marcos anteriores.

A ficha alimenta o combate, a progressão, a carga, o arrombamento e a conversa com Cameron.

## Personagens prontos (`premade/*.gcd`)

`CharacterFile` (`src/Import`) lê o formato GCD como o CE grava, em 432 bytes big-endian:

| Bytes | Conteúdo |
| --- | --- |
| 0–371 | bloco do critter (`protoCritterDataWrite`): flags, 35 atributos base, 35 bônus, 18 pontos de perícia, tipo de corpo, XP, tipo de morte, tipo de dano |
| 372–403 | nome |
| 404–419 | 4 perícias marcadas |
| 420–427 | 2 traits |
| 428–431 | pontos de personagem sem usar |

`--premade-report` lê os arquivos `.gcd` e `.bio` originais e compara as fórmulas
do projeto com os derivados que o editor gravou no GCD:

| Personagem | Arquivo | Traits | Perícias marcadas | Derivados conferidos |
| --- | --- | --- | --- | --- |
| Narg | `combat.gcd` | Heavy Handed, Gifted | Melee Weapons, Small Guns, Throwing | sem divergência |
| Mingan | `stealth.gcd` | Skilled, Small Frame | Sneak, Lockpick, Steal | sem divergência |
| Chitsa | `diplomat.gcd` | One Hander, Sex Appeal | Speech, Barter, First Aid | sem divergência |

Os derivados conferidos são HP, AP, AC, Melee Damage, carga, Sequence, taxa de
cura, crítico e resistências a radiação e veneno. Alguns valores resultantes:

- Narg: 44 HP e Melee 64%.
- Mingan: Lockpick 48% e Sneak 55%.
- Chitsa: Speech 70% e Barter 60%.

A ordem da escolha segue a tela original: combat, stealth, diplomat.

## Regras da ficha (`src/Runtime/PlayerCharacter.cs`)

Seguem o CE: `stat.cc`, `skill.cc` e `trait.cc`, sem perks e sem drogas ativas.

- **SPECIAL efetivo:** base mais o modificador de trait, limitado a 1–10.
  - Gifted: +1 em todos.
  - Bruiser: +2 ST.
  - Small Frame: +1 AG.
- **Derivados:** calculados do SPECIAL efetivo, mais os modificadores de trait.
  - Bruiser: −2 AP.
  - Kamikaze: AC 0 e Sequence +5.
  - Heavy Handed: Melee Damage +4.
  - Small Frame: carga −10 × ST.
  - Finesse: crítico +10.
  - Fast Metabolism: cura +2 e resistências a radiação e veneno zeradas.
- **Perícias:** valor-base + multiplicador × atributos + pontos.
  - Perícia marcada: +20%, com os pontos contados em dobro.
  - Gifted: −10% em todas.
  - Good Natured: +15 em First Aid, Doctor, Speech e Barter; −10 nas de combate.
- **Progressão:**
  - HP por nível: EN/2 + 2.
  - Pontos de perícia por nível: 5 + 2 × IN; Skilled soma 5 e Gifted tira 5.
  - Perk a cada 3 níveis; com Skilled, a cada 4.
- **Criação:**
  - 40 pontos de SPECIAL (35 + 5 a distribuir), todos usados;
  - até 2 traits distintos;
  - exatamente 3 perícias marcadas;
  - idade de 16 a 35;
  - nome de até 31 caracteres, o limite do GCD.

  Exigir os pontos todos e as 3 tags vem das mensagens do editor original. É
  inferência técnica, não conferida linha a linha no CE.
- **Perfil neutro:** SPECIAL 5, sem traits nem tags, com 5 pontos sem usar. Não é
  criável. Continua sendo o perfil dos testes automáticos e de checkpoints antigos.

## No mundo

- **Novo jogo:** `WorldSession.StartNewGame(character)` reinicia o mundo inteiro com o personagem; "Reiniciar prévia" mantém o personagem.
- **Combate e progressão:** `DerivedCombatStats.ForPlayer` e as sobrecargas de `CharacterProgression` usam a ficha.
- **Carga:** a capacidade vem da ficha: Narg carrega 250 lb, Mingan 100 lb.
- **Arrombamento:** usa a perícia Lockpick da ficha (Mingan 48%).
- **Conversa com Cameron:** o ramo de Speech exige o que o script exige.
  - O Node003 só oferece a fala 165 com Speech marcado (`metarule(40, 14)`), no nível de opções de IN 4+.
  - O Node015 aprova com "marcado ou rolagem bem-sucedida".
  - O preflight agora usa o IN e a tag da ficha em vez do IN 5 fixo. Chitsa (IN 7) entra; Narg e Mingan vão para o [duelo](temple-duel-2026-10-31.md).

## Checkpoint v12

O bloco `Character` guarda o tipo (pronto, criado ou neutro), o nome, o SPECIAL
base, os traits, as tags, os pontos de perícia, a idade, o sexo e o GCD de origem.

- **Personagem pronto:** precisa bater com o GCD atual. Uma ficha adulterada ou um arquivo trocado é recusado.
- **Personagem criado:** precisa cumprir as regras de criação.
- **Checkpoints v1 a v11:** restauram com o perfil neutro.

O v12 também traz o bloco `Duel` (ver o [duelo](temple-duel-2026-10-31.md)).

## Interface

- **Início:** ao abrir a prévia, a janela "Escolha do personagem" aparece. Nos testes automáticos ela não abre.
- **Ficha [N]:** abre a mesma janela durante o jogo. Começar reinicia o Templo.
- **Personagens prontos:** a janela mostra a ficha completa com a bio original.
- **Criação:** nome, idade, sexo, SPECIAL com os pontos restantes, traits (o terceiro fica desabilitado) e tags (a quarta fica desabilitada), com cada perícia já calculada. A pendência que impede de começar aparece explícita.
- **HUD:** mostra nome, SPECIAL efetivo e Arrombamento.
- **Bolsa:** mostra a Força do personagem.

## Validação

- **Testes sintéticos:** 7 grupos novos (`PlayerCharacterTests`).
  - Ficha neutra igual à anterior.
  - Leitor GCD e suas recusas.
  - Narg com Gifted e Heavy Handed.
  - Efeitos de cada trait.
  - Regras de criação.
  - Novo jogo com a ficha.
  - Checkpoint v12 e suas falsificações.
- **`--preview-test` (`CHARACTER_CREATION_OK`):**
  - N abre a janela, e os três personagens aparecem na ordem original com a bio;
  - o editor recusa um personagem incompleto e impõe os limites;
  - começar reinicia o mundo;
  - um personagem criado sobrevive a salvar e carregar no v12;
  - Narg entra com a ficha original e o Speech bloqueado;
  - o perfil neutro volta no fim.

- **`tools/Validate-Temple.ps1` (21 passos) e `Run-QuietGodot.ps1`:** aprovados; capturas em `.local/captures/characters2/`.

## Fora do escopo

- Gastar pontos de perícia e escolher perks ao subir de nível.
- Traits com efeitos fora da ficha: One Hander, Fast Shot, Bloody Mess, Jinxed, Chem Reliant, Chem Resistant e Sex Appeal (reação). Hoje eles aparecem na ficha sem efeito.
- Partes de traits que agem dentro do combate, ainda não aplicadas:
  - Finesse: +30% de resistência do alvo, mas o crítico +10 já entra;
  - Heavy Handed: crítico pior, mas o Melee Damage +4 já entra; o projeto ainda não tem críticos.
- Tag! e Educated (perks), além de dificuldade diferente de Normal.
- Idade e sexo não mudam regras; só aparecem na ficha.
