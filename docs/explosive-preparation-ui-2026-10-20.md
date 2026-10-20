# Preparação do explosivo na interface — 20/10/2026

Branch `codex/explosive-placement-ui`, a partir de `e86cd1a`.

Depois de recolher o explosivo original do pote 358 na elevação 1, abra **B**,
selecione o item e use **Posicionar · ensaio**. Escolha entre 10 e 180 segundos
em passos de 10 (padrão 60), depois confirme **Posicionar 1 unidade** ou cancele.

A janela informa que este é um ensaio com preparação bem-sucedida e tempo
pausado. Não faz rolagem real de Traps, não avança o prazo pela caminhada ou
pelos frames e não dispara uma explosão automática. A resolução da barreira
continua disponível somente na auditoria controlada existente. O rótulo no
chão agora também informa “pausado”.

O botão usa a proposta existente, vinculada ao PRO/hash original. Confirmar
revalida o plano e retira exatamente uma unidade da bolsa para sua alocação
posicionada, preservando a identidade fonte e criando um GUID separado.
Movimento, inspeção de outra elevação, saída, item indisponível e plano obsoleto
impedem a operação. Cancelar ou fechar pelo X devolve à bolsa sem transferência.

A preparação é um modal: não permite caminhar, mudar câmera/elevação ou usar
atalhos do mundo enquanto estiver aberta. Reconstruir o mundo ou carregar fecha
essa janela. Um callback antigo ou repetido não pode atuar em outra sessão.
Após posicionar, **K/R** preservam o mesmo GUID e o prazo parcial pelo checkpoint
v4 já existente; nenhum novo formato de persistência foi criado.

## Verificação

O teste `EXPLOSIVE_PREPARATION_UI_OK` começa após o percurso e a coleta reais
do item 364/PID 85, sem inserir artificialmente a unidade na bolsa. Cobre:

- Ação da bolsa, prazos 10/60/180, confirmação com valor digitado e cancelamento/X.
- Atalhos e frames no modal; recusas por movimento e inspeção de outra elevação.
- Posição alterada entre proposta/confirmação, callback após carregar e repetição.
- Uma unidade posicionada, identidade e navegação originais preservadas.
- Salvamento/carregamento do mesmo GUID com 75 ticks restantes após avanço
  explícito de teste; frames normais mantêm esse prazo.
- Restauração integral da fixture antes da auditoria anterior de efeito/viagem.

O runner isolado aceita `-Check ExplosivePreparation` para repetir somente essa
bateria e a auditoria de efeito já existente. Capturas foram inspecionadas em
1440×900, 1280×720 e 1024×768. A bateria completa exige o novo marcador.
Os helpers de percurso desse ensaio passam pela conclusão normal da caminhada,
mantendo rota, Parar e Salvar consistentes nas capturas.

Resultado: build sem avisos/erros, **225 grupos sintéticos** e **13/13 etapas**
da bateria completa aprovados. Manifesto:
`.local/temple-validation-20261020-103458-a4a6355b/manifest.json`.
Os runners gráficos encerraram com código zero, saída de erro vazia e desktop
de entrada `Default` preservado. As mensagens deliberadas de preferências
bloqueadas/JSON incompleto pertencem aos testes anteriores de recuperação.

Os dados locais e o CE fixado mantêm os caminhos já registrados no projeto.
Nenhum asset proprietário entra no Git. A mudança de indentação preexistente
em `TemplePreview.cs` e os arquivos `.uid` locais foram preservados separadamente.

## Próximo marco

Avanço explícito do tempo pausado pela interface, com tratamento de eventos
pendentes antes de habilitar a resolução do efeito. Manter a operação restrita
ao perfil validado e sem combate geral. Recomendo GPT-6.1 Sol, esforço Alto.
