# Marco 49 — proposta de uso sem alterar a sessão

`ExplosiveUseProposal.Create` valida uma unidade efetivamente carregada e devolve
um plano, sem executar uso. Aceita o PRO original PID 85 pelo hash/size/subtipo
confirmados; recusa origem obsoleta, protótipo modificado, caminhada em curso,
tile de saída, prazo e resultado de rolagem inválidos. Usa os dados atuais da bolsa,
sem confiar na quantidade de uma linha antiga da interface. O plano inclui origem,
local do personagem, PID ativo 209, prazo e perfil CE padrão de dano 40–80/raio 3.

Decisões de compatibilidade explícitas: cancelar não altera propriedade; o plano
define o PID armado e seus valores de dano diretamente, sem propagar a consulta
incompleta da referência CE descrita no marco 45. Overrides de Sfall continuam
fora desse perfil. O resultado recebido de habilidade é fornecido pelo chamador;
o módulo não faz uma rolagem nem usa tempo real.

Nenhum botão de uso foi habilitado. O plano não retira, larga, consome ou explode
o item, não remove a barreira e não concede XP. Ainda faltam commit atômico com
nova verificação de estado, relógio, dano/segurança de criaturas, objeto criado e
checkpoint correspondente; um plano antigo não deve ser aplicado diretamente.

Validação: build sem avisos/erros e 167 grupos sintéticos aprovados. Conferidos
conservação integral do checkpoint, identidade/quantidade atual, cancelamento,
recusas, falha/crítica e Demolition Expert. `--explosive-profile-report` também
valida a proposta numa sessão isolada semeada a partir da unidade original ID 364,
com estado serializado idêntico antes/depois. A semeadura é fixture de teste,
não uma coleta jogável nem uma escrita no slot do usuário.

Próximo: projetar o commit de propriedade e checkpoint antes da ativação na prévia.
GPT-6.1 Sol, esforço Alto.
