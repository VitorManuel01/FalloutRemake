# Próximo incremento: saída do NPC na transação do mundo

A base ensaiada já preserva pose, bloqueador, porta, fase e evento em um registro.
A próxima integração precisa incluir também as escritas Speech aceitas do perfil
verificado no mesmo candidato do mundo. Apenas mover a malha deixaria interações
e navegação consultando a posição original.

Contrato antes de habilitar efeitos na partida:

1. Preparar um candidato do mundo vinculado à sessão/revisão, aos hashes das
   fontes e ao perfil de personagem suportado. Confirmar proximidade, repouso,
   ramo Speech completo e ausência de desafio/combatente incompatível.
2. Projetar o ator pela chave original em navegação, seleção, aproximação,
   visibilidade e consultas de alcance. O grafo dele exclui somente seu próprio
   bloqueador e usa a política de saídas para NPC; o jogador mantém a sua.
3. Manter pose, bloqueador, porta, variáveis e evento em um checkpoint único.
   Caso seja necessário v6, migrar v1–v5 sem inventar aceitação ou saída anterior.
   A restauração deve validar tudo antes de substituir o mundo ativo.
4. Revalidar ocupação antes de cada hex e tratar falhas de porta/movimento sem
   teletransporte. Espera de 20 ticks não pode herdar automaticamente o relógio
   manual do explosivo. Definir pausa, troca de área e retomada explicitamente.
5. Cobrir falha no último componente, arquivo inválido, ações antigas,
   ocupação no percurso, NPC oculto, retorno entre áreas e preservação dos
   itens/25 XP existentes. Só então adicionar o controle de aplicação ativa.

O relatório real, o ensaio e o arquivo separado permanecem úteis como referência
e regressão. Não são um checkpoint original SAV nem justificam implementar
combate, custódia do duelo ou o local 16 não declarado.

Modelo recomendado para essa integração: GPT-6.1 Sol, esforço Alto.
