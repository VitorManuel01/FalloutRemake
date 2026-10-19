# Checkpoint v4 e aplicação do efeito da barreira

`WorldSession.CommitBarrierEffect` revalida a proposta vencida, token/revisão,
protótipo resolvido pelo mundo, identidade nova e disponibilidade do alvo.
Prepara o registro e confere o limite de serialização antes de consumir a unidade.
A operação então marca somente o uso causador como consumido e instala a
substituição. Nenhuma validação, callback ou leitura ocorre entre as duas mudanças
no trecho final, que roda na thread principal. Reaplicação não consome outra unidade.

`ScenerySession` guarda uma sobreposição separada dos objetos MAP. O perfil atual
admite somente a barreira conhecida. A navegação de portas e viagens consulta a
mesma sobreposição, ocultando o bloqueador original; o substituto verificado é
NoBlock, sem script e sem comportamento de saída. O registro novo tem GUID próprio,
GUID causador, chave original, posição, PID/FID e hash do protótipo. Não concede XP.

O checkpoint atual é v4/`neutral-v4`, com `Replacements` obrigatório por área.
v1–v3 continuam aceitos e migram para lista vazia. Todos os campos dos registros
novos são obrigatórios no JSON. IDs duplicados, causa inexistente/não consumida,
perfil alterado, posição/range incompatíveis e áreas sem suporte são recusados.
A causa não pode estar no próprio hex originalmente bloqueado: isso impediria
que um checkpoint forjado justificasse circularmente sua própria abertura.

O PRO de substituição é resolvido explicitamente pelo catálogo local, pois não
pertence aos protótipos presentes no MAP original. Carregar uma substituição sem
esse recurso compatível falha antes de substituir a sessão ativa. O arquivo guarda
somente metadados; nenhum byte de asset é adicionado ao checkpoint ou ao Git.

A restauração prepara todas as áreas/inventários, valida as causas, aplica as
sobreposições na sessão candidata e só então valida posições de usos/personagens.
Assim, salvar em cima do antigo bloqueador e carregar funciona. Origem do item,
mapa da explosão e área ativa podem ser diferentes. Reset remove a sobreposição.

Validação: 224 grupos sintéticos, com commit, concorrência, token antigo, recurso
inválido/ausente, array/campos ausentes, registros forjados, migração v3, posição
no antigo bloqueador, novo uso nesse hex, área inativa e origem em outro mapa.
Build sem avisos/erros. Não há combate geral, relógio de parede ou botão de uso.

Próximo: ensaio sobre os dados originais e visualização da passagem após salvar e
recarregar. GPT-6.1 Sol, esforço Alto.
