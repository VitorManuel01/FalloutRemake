# Entrada de Arroyo: karma e reputação persistentes

A chegada original 3→4, elevação 0, hex 20517, após Speech aceito, agora
registra conjuntamente **600 XP**, **karma 15**, **reputação de Arroyo 65** e
classificação **G41/Wanderer**. G47 parte de 50 no GAM resolvido. Não houve
repetição dos incrementos para viagens, inspeção de saída ou carregamento.

O adaptador confere os hashes originais e as instruções de G0+=15, limpeza e
seleção da classificação e G47+=15. Continua limitado ao personagem neutro e
à chegada suportada. A preparação termina antes de mover o personagem ou
registrar a visita: erro de fonte preserva a partida e todos os efeitos.

Checkpoint **v9/neutral-v9** guarda `Entries.ArroyoGlobals` explicitamente,
incluindo fingerprint e valores. Valores, origem ou campos adulterados são
recusados antes de substituir a sessão. Reset limpa recompensa e globais.
O painel em Arroyo mostra karma/reputação; o histórico do templo permanece.

Migração:

- v8 com recibo válido de 600 XP deriva o subconjunto exato de globais desse
  evento neutro; não concede XP adicional. A próxima gravação usa v9.
- v8 sem recibo e vila já visitada permanece sem recompensa/globais inferidos.
- v1–v7 mantêm a política conservadora de visitas. Uma vila ainda não carregada
  pode receber o efeito quando ocorrer a primeira chegada real suportada.

Testes cobrem ida/volta, repetição, gravação/restauração, migração de JSON v8
sem o novo campo, fonte indisponível, reset, campos ausentes e karma/reputação,
classificação/fingerprint adulterados. O cenário Godot também verifica os
valores na chegada real e recusa um checkpoint com reputação 80 preservando
a sessão ativa. O marcador exigido é `ARROYO_ENTRY_GLOBALS_OK`.

Validação concluída: **323 grupos sintéticos, 20/20 etapas**, builds sem avisos
ou erros, manifesto `.local/temple-validation-20261021-160542-56486738/manifest.json`.
A captura `.local/arroyo-globals-v9-1280.arroyo-return-no-duplicate.png` mostra
karma 15/reputação 65 e XP 625 após retorno e carga. O runner isolado passou
em 29,8 s; desktop de entrada `Default` antes e depois.

Isso não executa filmes, clima, um sistema geral de reputação ou a conversa
ativa da anciã. Próximo incremento: orientar a caminhada até sua identidade
original e verificar alcance antes de uma consulta de diálogo.
Modelo recomendado: **GPT-6.1 Sol, esforço Alto**.
