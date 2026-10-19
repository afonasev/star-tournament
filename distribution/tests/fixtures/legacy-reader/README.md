# Выпущенные Source readers

Namespace-only копии `distribution/launcher/AuthenticatedSource.cs` из:
- production0.1.5: source/tag d9c985a0df995abd783ce6661174b835391c55f8, SHA2566913c81de58e98fb9011d48388cdb54f6427b877c351d32f9cca6537acdc5d72;
- test0.1.5-test.1: package custody source23b21e95cf7373293078dff3363053eb6359a6a7, tag eab6bdd310b1e24be7e24ef499bc9c261617b5da, reader source identical at both refs, SHA25694acf706c416a9671752a2221a8d94bef9297a9905d4fb17bd6d19e8a30869f0.

Изменена только строка namespace, чтобы исполнить оба настоящих reader рядом с текущим кодом. Logic не адаптирована под новые expectations. Velopack1.2.158 совпадает с выпущенными project refs. Исходные signatures/identity и source provenance — docs/evidence/standard-release-compatibility/transition/. Тесты offline transport+fixture RSA key, не запуск установленных бинарников или готового bridge.
