# Marco 46 — arte na consulta do inventário original

A consulta I mostra ícones na árvore e a imagem do item selecionado junto dos
dados originais. Exibe separadamente FID do objeto e FID de inventário. Reusa
o catálogo/cache de J/B e a ausência da arte continua sem impedir a consulta.

A consulta mantém o conteúdo gravado no MAP mesmo após coletá-lo na sessão.
Não oferece transferência ou uso. Isso permite conferir a chave no inventário
original do ator da elevação 2, sem movê-la para a bolsa ou transportar o personagem.
O nome de protótipo do ator continua preservado, como já documentado no marco 36.

Validação: build sem avisos/erros e suíte Godot completa sem janela com saída 0.
Conferidos ícone da lança (FID 0x0700000D), pó original após coleta na sessão,
Temple Key (0x0700012F), layout e conservação da posição/bolsa/fonte.
`INVENTORY_ART_UI_OK` inclui a inspeção da chave; `INVENTORY_INSPECTOR_OK`
continua cobrindo atalhos, modal e isolamento da caminhada.

Captura pelo runner isolado, sem janela no desktop ativo. Assets fora do Git.
Próximo: revisar a integração recente e fechar a bateria com registros de
validação atualizados. GPT-6.1 Sol, esforço Alto.
