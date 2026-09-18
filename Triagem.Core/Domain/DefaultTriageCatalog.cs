namespace Triagem.Core.Domain;

public sealed record DefaultQuestion(
    string Text,
    int Weight,
    string? Category = null,
    IReadOnlyList<string>? Options = null);

public sealed record DefaultTriage(
    string LegacyTitle,
    string Title,
    string Audience,
    string Icon,
    string Description,
    IReadOnlyList<DefaultQuestion> Questions);

/// <summary>
/// Catálogo único das triagens distribuídas pela API e pelo modo offline.
/// O conteúdo é um roteiro de rastreio orientativo e não constitui diagnóstico.
/// Alterações clínicas devem ser homologadas por profissional responsável antes da publicação.
/// </summary>
public static class DefaultTriageCatalog
{
    public const int Version = 3;
    public const bool ClinicallyValidated = false;
    public const string ValidationNotice =
        "Protótipo acadêmico ainda não homologado clinicamente. O resultado é educativo e não deve orientar diagnóstico, tratamento ou urgência.";

    public static IReadOnlyList<DefaultTriage> Items { get; } =
    [
        new(
            "Protocolo de Triagem Fonoaudiológica Integrada",
            "Protocolo de Triagem Fonoaudiológica Integrada",
            "Adultos e idosos",
            "🗣️",
            "Protocolo orientativo de deglutição, linguagem e cognição, voz, audição e equilíbrio. Não substitui avaliação fonoaudiológica.",
            [
                new("1. Apresenta dificuldade para mastigar algum alimento?", 2, "Deglutição"),
                new("2. Sente o alimento parado na garganta?", 3, "Deglutição"),
                new("3. Apresenta tosse durante ou após a deglutição?", 3, "Deglutição"),
                new("4. Sente cansaço durante as refeições?", 2, "Deglutição"),
                new("5. Precisa beber líquidos para ajudar o alimento a descer?", 1, "Deglutição"),
                new("6. A saliva escorre pela boca?", 3, "Deglutição"),
                new("7. Engasga com:", 1, "Deglutição", ["Líquidos", "Pastosos", "Sólidos", "Saliva"]),
                new("8. Apresenta dificuldade para memorizar coisas do dia a dia?", 2, "Linguagem e Cognição"),
                new("9. Atende comandos básicos: 'pegue o copo', 'guarde seu sapato'?", 3, "Linguagem e Cognição"),
                new("10. Consegue manter uma conversa por aproximadamente 5 minutos?", 2, "Linguagem e Cognição"),
                new("11. Lembra o nome dos objetos do dia a dia?", 2, "Linguagem e Cognição"),
                new("12. Sabe onde está (local e data)?", 3, "Linguagem e Cognição"),
                new("13. Apresenta voz rouca?", 1, "Voz"),
                new("14. Toma bastante água ao longo do dia?", 1, "Voz"),
                new("15. Fica cansado ao falar?", 1, "Voz"),
                new("16. Tem pigarro com frequência?", 2, "Voz"),
                new("17. Queixa-se de tontura ou zumbido frequentemente?", 1, "Audição e Equilíbrio"),
                new("18. Queixa-se de tontura ou zumbido frequentemente?", 1, "Audição e Equilíbrio"),
                new("19. Pede para repetirem o que foi falado?", 1, "Audição e Equilíbrio"),
                new("20. Consegue perceber sons altos?", 1, "Audição e Equilíbrio")
            ]),
        new(
            "Triagem em Saúde Mental",
            "Triagem de Linguagem e Cognição",
            "Adultos e idosos",
            "🧠",
            "Rastreio orientativo de alterações percebidas na linguagem, comunicação funcional e cognição. Não substitui avaliação fonoaudiológica.",
            [
                new("Tem dificuldade frequente para encontrar palavras durante uma conversa?", 2),
                new("Percebe que troca palavras ou usa palavras inadequadas sem notar?", 2),
                new("Tem dificuldade para compreender frases, instruções ou conversas?", 3),
                new("Perde o fio da conversa ou tem dificuldade para organizar o que deseja dizer?", 2),
                new("Notou mudança recente na leitura ou na compreensão de textos?", 2),
                new("Notou mudança recente na escrita de palavras ou frases?", 2),
                new("Esquece informações recentes a ponto de prejudicar sua comunicação diária?", 2),
                new("Tem dificuldade para reconhecer ou nomear pessoas e objetos conhecidos?", 3),
                new("Familiares perceberam mudança recente na sua fala ou compreensão?", 3),
                new("Essas dificuldades interferem em atividades sociais, profissionais ou domésticas?", 2),
            ]),
        new(
            "Triagem em Saúde Infantil",
            "Triagem Fonoaudiológica Infantil",
            "Crianças de 0 a 12 anos",
            "🧒",
            "Rastreio orientativo de fala, linguagem, audição, alimentação e comunicação na infância. Não substitui avaliação fonoaudiológica.",
            [
                new("A criança fala ou se comunica menos do que o esperado para sua idade?", 3),
                new("Tem dificuldade para compreender ordens ou perguntas adequadas à idade?", 3),
                new("Pessoas fora da família têm dificuldade frequente para entender sua fala?", 2),
                new("Troca, omite ou distorce sons da fala de forma persistente?", 2),
                new("Apresenta repetições, bloqueios ou esforço para falar?", 2),
                new("Parece não responder quando é chamada ou pede repetição com frequência?", 3),
                new("Teve otites recorrentes ou suspeita de redução auditiva?", 2),
                new("Tem dificuldade persistente para mastigar, engolir ou aceitar diferentes alimentos?", 2),
                new("Tem dificuldade para interagir, manter turnos de conversa ou brincar comunicativamente?", 2),
                new("As dificuldades de comunicação prejudicam a aprendizagem ou a convivência?", 2),
            ]),
        new(
            "Triagem em Saúde da Mulher",
            "Triagem de Motricidade Orofacial",
            "Todas as idades",
            "👄",
            "Rastreio orientativo de respiração, mastigação, deglutição e funções orofaciais. Não substitui avaliação fonoaudiológica.",
            [
                new("Respira pela boca durante boa parte do dia ou enquanto dorme?", 2),
                new("Ronca com frequência ou acorda com a boca seca?", 2),
                new("Tem dificuldade ou cansaço para mastigar alimentos?", 2),
                new("Mastiga preferencialmente de um único lado?", 1),
                new("Engasga, tosse ou sente alimento parado ao engolir?", 3),
                new("Derrama alimento ou saliva pela boca sem perceber?", 2),
                new("Percebe a língua empurrando os dentes ao falar ou engolir?", 1),
                new("Sente dor, estalo ou limitação ao abrir a boca?", 2),
                new("Apresenta dificuldade persistente para articular algum som da fala?", 2),
                new("Alguma dessas alterações prejudica alimentação, sono, fala ou qualidade de vida?", 2),
            ]),
        new(
            "Triagem em Saúde do Idoso",
            "Triagem Auditiva do Idoso",
            "Pessoas com 60 anos ou mais",
            "👂",
            "Rastreio orientativo de dificuldades auditivas e impacto funcional na comunicação da pessoa idosa. Não substitui avaliação audiológica.",
            [
                new("Pede para as pessoas repetirem o que disseram com frequência?", 2),
                new("Aumenta o volume da televisão ou do telefone mais do que outras pessoas?", 2),
                new("Tem dificuldade para acompanhar conversas em grupo?", 2),
                new("Tem dificuldade para entender fala em locais com ruído?", 3),
                new("Tem dificuldade para ouvir ao telefone?", 2),
                new("Percebe zumbido frequente em um ou nos dois ouvidos?", 1),
                new("Deixa de participar de conversas ou encontros por dificuldade para ouvir?", 3),
                new("Familiares dizem que parece não ouvir ou responder adequadamente?", 2),
                new("Percebe piora recente ou diferença de audição entre os ouvidos?", 3),
                new("A dificuldade auditiva reduz sua autonomia ou segurança no dia a dia?", 3),
            ]),
        new(
            "Triagem Respiratória",
            "Triagem de Voz",
            "Profissionais da voz e adultos",
            "🗣️",
            "Rastreio orientativo de alterações vocais, esforço e impacto do uso da voz. Não substitui avaliação fonoaudiológica ou otorrinolaringológica.",
            [
                new("Apresenta rouquidão ou mudança de voz por mais de duas semanas?", 3),
                new("Sente cansaço vocal após falar por pouco tempo?", 2),
                new("Precisa fazer esforço para a voz sair?", 2),
                new("Sente dor, ardor ou desconforto ao falar?", 2),
                new("A voz falha ou desaparece durante o dia?", 2),
                new("Percebe perda de alcance, potência ou controle da voz?", 2),
                new("Pigarreia ou tosse repetidamente durante o uso da voz?", 1),
                new("Tem dificuldade para ser ouvido em ambientes comuns?", 2),
                new("Usa a voz profissionalmente e os sintomas prejudicam seu trabalho?", 2),
                new("A alteração vocal surgiu de forma súbita ou vem piorando?", 3),
            ]),
        new(
            "Triagem Clínica Geral",
            "Triagem Auditiva",
            "Todas as idades",
            "🎧",
            "Rastreio orientativo de sinais de dificuldade auditiva e necessidade de avaliação audiológica. Não substitui diagnóstico profissional.",
            [
                new("Tem dificuldade para ouvir fala em volume habitual?", 2),
                new("Pede repetição ou entende palavras diferentes das que foram ditas?", 2),
                new("Tem dificuldade para compreender fala em ambientes ruidosos?", 3),
                new("Aumenta frequentemente o volume de televisão, celular ou fones?", 2),
                new("Percebe zumbido frequente em um ou nos dois ouvidos?", 1),
                new("Percebe diferença de audição entre os ouvidos?", 3),
                new("Teve infecções de ouvido recorrentes, dor ou secreção?", 2),
                new("Foi exposto com frequência a ruído intenso ou som alto?", 2),
                new("A dificuldade para ouvir prejudica estudo, trabalho ou convivência?", 2),
                new("Houve perda auditiva súbita ou piora importante recente?", 3),
            ]),
    ];
}
