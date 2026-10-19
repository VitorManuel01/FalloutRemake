# Prévia da barreira aplicada e recarregada

A sobreposição salva em v4 agora aparece no Godot. O volume original da barreira
continua presente com seus metadados/ID 44 e na contagem de objetos importados,
mas fica invisível e sem seleção física. A opção de mostrar objetos ocultos não
ressuscita essa representação substituída.

Uma camada própria desenha entulho baixo, não bloqueador, com o rótulo
“Passagem aberta”. Seu GUID, GUID causador, chave substituída e PID/FID ficam
separados dos IDs MAP. Não recebe ID original nem é contado como raiz importada.
O filtro Cenário oculta o desenho sem alterar a passagem. O substituto só aparece
na elevação correta; reset remove o estado e carregar recria a mesma identidade.

Após commit validado, a prévia reconstrói navegação, alcance, seleção e caches de
percurso sobre a geometria atual. A posição do personagem não muda durante o
efeito. Não é instalado um snapshot antigo da projeção. A ação de uso continua
sem botão; este incremento liga o efeito já validado ao renderer e à integração.

`APPLIED_BARRIER_PREVIEW_OK` estende o ensaio com coleta real do explosivo:
aplica a substituição, remove a forma posicionada consumida, testa filtros e
save/reset/load sobre o antigo bloqueador. Caminha pela saída original para a
elevação 2, recolhe as duas unidades do baú 376, recarrega e confere a bolsa.
Retorna à elevação 1, salva novamente em 14322 e recarrega. Conserva a substituição,
os itens, os 25 XP de arrombamento e os bytes originais. O baú com script continua
vazio e indisponível. A legenda de entidades e os estados dos recipientes seguem
aprovados no mesmo ensaio.

Validação: build sem avisos/erros e integração headless aprovada. Capturas somente
por `Run-QuietGodot.ps1`, em desktop isolado, sem fallback para janela comum.

Bateria final: 13/13 etapas, 224 grupos sintéticos e nove relatórios reais.
Captura gráfica aprovada: saída 0, stderr vazio, 29,6 segundos, desktop de entrada
Default antes/depois. Passagem aberta e bolsa da elevação 2 revisadas visualmente.

Próximo: revisar os achados visuais externos, se enviados, e depois planejar a
interação de temporizador/ativação com regras explícitas de avanço de tempo.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
