# Bateria Nature e primeira entrada de Arroyo

Branch `codex/nature-assets`, mantendo as alterações anteriores do usuário.

| Marco | Resultado |
| --- | --- |
| Nature: pedras | 23 rochas no exterior e 30 em Arroyo usam RiverRock1/RockS01, em três lotes por área. |
| Primeira entrada / checkpoint v8 | Entrada efetiva separada do cache; Speech concede 600 XP uma vez, com migração conservadora dos saves antigos. |
| Árvores do exterior | 88 árvores originais usam o modelo Nature queimado, em um lote compartilhado adicional. |
| Retorno real ao templo | Botão de orientação, caminhada e Enter nativos; segunda chegada à vila conserva 625 XP e os dois itens do teste. |
| Consulta da malha | Parte alta dos troncos seleciona o corpo e ID original; navegação continua baseada nos hexes originais. |
| Preferência Nature | Opção salva em preferências visuais v2; v1 continua legível sem regravação automática. |
| Modelo ausente | Formas provisórias continuam selecionáveis; modelos recuperam após reconstrução quando voltam a estar disponíveis. |
| Próximo diálogo: base estática | Instância e programa originais da anciã localizados; dependências de estado identificadas para auditoria, sem habilitar diálogo genérico. |

Somente `Assets/Nature` é carregado pelas novas integrações. Assets, texturas e
exportações originais permanecem locais e fora do Git. A árvore disponível é
um tronco/árvore queimada, sem copa ou folhas. Paredão e pedregulho ainda não
receberam uma correspondência visual no mapa; outras categorias não são usadas.

Dados originais, IDs, bloqueios por hex, inventário e scripts permanecem
separados da apresentação. Filmes, reputação, outros ramos de recompensa,
perks, níveis e combate geral ainda não são implementados pelo adaptador.

Documentação por marco:
[pedras](nature-models-2026-10-21.md),
[primeira entrada](arroyo-first-entry-v8-2026-10-21.md),
[árvores](nature-exterior-trees-2026-10-21.md),
[retorno](arroyo-return-loop-2026-10-21.md),
[seleção](nature-mesh-picking-2026-10-21.md),
[preferências](nature-visual-preferences-2026-10-21.md),
[recuperação](nature-asset-recovery-2026-10-21.md).
[Base da anciã](arroyo-elder-baseline-2026-10-21.md).

Validação final: **321 grupos sintéticos**, **19/19 etapas**, builds sem avisos/
erros, `.local/temple-validation-20261021-112858-3e249650/manifest.json`.
Capturas isoladas finais: `.local/nature-recovery-isolated.json`,
`.local/arroyo-return-isolated.json` e `.local/nature-preferences-startup-isolated.json`,
todas aprovadas com entrada `Default` preservada. O teste de nova abertura
confirma a preferência Nature desativada sendo lida em um novo processo.

Capturas e testes gráficos foram executados somente no desktop isolado.
As instâncias e janelas do usuário não foram alteradas. Sem push.

Próximo marco recomendado: auditar o estado original de entrada de Arroyo
necessário ao ramo inicial da anciã, antes de habilitar uma conversa ativa adicional.
**GPT-6.1 Sol, esforço Alto**.
