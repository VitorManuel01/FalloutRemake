# Estado original do desafio e configuração global

O caminho da configuração do jogo foi conferido em `game.cc` do CE local:
`data/vault13.gam`, seção `GAME_GLOBAL_VARS:`. Não é um GAM de variáveis de um mapa.
O leitor preserva as linhas implicitamente zeradas contadas pelo motor; não
compacta comentários indentados ou linhas com espaços. Entradas ambíguas e
fora do perfil escalar suportado são recusadas.

Na instalação Epic, a configuração resolvida vem do patch, hash
`724464005C0B99AADA0E7808A2D13E7C67CDF7A3FC06EF50010DEC133BF42B12`.
A global de índice 10 é `GVAR_START_ARROYO_TRIAL`, inicial **0**.

`--temple-state-report` constrói blocos separados a partir do MAP, dos metadados
16/9 e dessa global. A inicialização conhecida da porta é aplicada apenas ao
armazenamento destacado; uma transação que tenta local 16 do NPC é recusada
inteira. Não inicia diálogo, custódia ou execução geral de scripts.

A auditoria de geometria mostrou que o NPC não tem vizinho alcançável a partir
da chegada à elevação 2 enquanto a porta final permanece fechada. O próximo
incremento deve suportar o estado inicial comprovado dessa porta antes de
oferecer aproximação ou conversa com o NPC.

Validação: **257 grupos sintéticos** (+3 do leitor GAM), relatório real com
reconstrução do MAP e auditoria do percurso após a barreira. O novo relatório
passa a integrar a bateria, agora com **16 etapas**. Sem texto/bytes originais
de configuração no Git e sem alterar a configuração instalada.

Próximo: abertura/fechamento da porta final no perfil inicial, sem arrombamento
ou estados do desafio, com persistência e migração explícitas.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
