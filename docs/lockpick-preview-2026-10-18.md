# Oitavo marco — personagem mínimo e Lockpick na prévia

Branch `codex/temple-lockpick-preview`, base `bfe3faa`.

## Como usar

**F5 → M → clique na primeira porta → U**. Depois, clique na segunda porta do
corredor para se aproximar. Quando o marcador parar, use **L / Arrombar**, seguido
de **U / Abrir porta**. A passagem pode ser atravessada. O painel mostra os XP.

L vira **Retrancar** quando a fechadura está destrancada, conforme o script analisado.
Não concede XP novamente. Trocar de elevação preserva portas e XP; **Reiniciar prévia**
restaura as portas, a tranca inicial, o marcador, os XP e o histórico da rolagem.
Movimento, distância, outra elevação e fechadura emperrada impedem novas tentativas.

## Perfil e rolagem

Perfil próprio de teste, sem importar ou representar um personagem original:
**Percepção 5, Agilidade 5, Sorte 5**. Dificuldade normal, sem pontos treinados,
perícia marcada, traits, perks ou grupo. A fórmula CE resulta em Lockpick
`10 + Percepção + Agilidade = 20`; chance crítica derivada da Sorte é 5.

O resolvedor recebe a solicitação da porta: perícia 9, modificador **+100**. Sorteia
um inteiro uniforme de 1 a 100 e compara a margem `20 + 100 − d100`. Margem zero
conta como sucesso. Não introduz falha automática no resultado 100. Logo, **todas as
100 faces têm sucesso nesta porta com este perfil**, e a recompensa continua 25 XP
uma vez. Isso não afirma que todo bônus +100 sempre garanta sucesso em outros perfis.

A prévia fica no primeiro dia, sem relógio de jogo em andamento. Conforme o limite
padrão CE, não faz o segundo sorteio de crítico nesse período. O tradutor também
cobre o modo elegível a críticos para testes: falha crítica quando o segundo d100 é
`≤ −margem/10`; sucesso crítico quando é `≤ margem/10 + chanceCrítica`, com divisão
inteira. Um resolvedor separado mantém a tradução e as regras da porta independentes.

Usa `Random.Shared` para os percentis; não reproduz a sequência global de números
aleatórios do CE ou de um save. A prévia não avança dias nem habilita a opção Sfall
que remove o limite temporal dos críticos.

## Integração

O adaptador é ligado somente quando o importador fornece os hashes e vínculos
conferidos. Para dados diferentes, a cena mantém a inspeção e o bloqueio das portas
com scripts desconhecidos. `CanLockpick` consulta disponibilidade sem sortear.

O clique numa porta trancada reconhecida agora encontra um hex vizinho alcançável.
O botão fica indisponível durante a aproximação e muda de ação após resolver a tranca.
Destrancar não muda a ocupação: a abertura explícita reconstrói a navegação.
O painel exibe perfil, perícia, XP e resultado do d100. A fonte continua preservada.

## Validação

- Compilação Godot .NET sem erros/avisos; **64 grupos sintéticos**, 7 novos sobre
  perfil, limites do d100, críticos, entradas inválidas e resolvedor.
- Todas as 100 faces testadas com o perfil e bônus atuais; um único sorteio por
  tentativa no primeiro dia. Igualdade, truncamento e limiares críticos conferidos.
- `LOCKPICK_PREVIEW_OK` nos testes Godot sem janela e gráfico, ambos com saída 0.
- Clique projetado pela câmera aproxima da segunda porta; L usa a rolagem real do
  perfil, e o sinal do botão testa a retranca. +25 XP uma vez, U, travessia, fechamento
  ocupado, troca de elevação e reinício validados com o MAP Epic.
- Um resultado crítico injetado **somente no teste** verifica o bloqueio do botão e
  da tecla sem consumir outro sorteio. A interface normal sempre usa o resolvedor.
- Fonte reconstruída byte a byte. Prévia, navegação e primeira porta continuam
  passando. Captura 1440×900 revisada: `.local/temple-lockpick.lockpick.png`.

Comandos do README: `--preview-test` emite agora também `LOCKPICK_PREVIEW_OK`.
Logs: `.local/character-self-tests.log`, `.local/lockpick-preview-test.log`,
`.local/lockpick-render*.log`. Dados e imagens permanecem fora do Git.

## Referência, limites e próximo passo

Referência CE local `e97087b9582f37075db347a89898887320753f8b`:
`skill.cc` (`gSkillDescriptions`, `skillGetValue`), `stat.cc` (crítico derivado da
Sorte) e `random.cc` (`randomRoll`, `randomTranslateRoll`), além da análise anterior
do `doorl100.int`. As fórmulas e condições foram comparadas ao código, sem afirmar
uma comparação jogável lado a lado com o executável original; esta segue pendente.

Ainda não há criação de personagem, GCD, progressão completa, inventário, armadilhas
ativas, combate ou transição de mapa. O perfil é fixo e explicitamente de teste.
Próximo: mapear o destino da saída após a segunda porta e implementar uma transição
mínima preservando o personagem e o estado da sessão.
