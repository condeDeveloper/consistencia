namespace Conde.Consistencia;

/// <summary>
/// As GARANTIAS MAIS FRACAS, que sao as que os sistemas de verdade entregam.
///
/// Linearizabilidade e caro: ela exige coordenacao em toda operacao. Quase todo
/// armazem distribuido entrega alguma coisa mais fraca, e o nome dessa coisa
/// importa, porque cada uma proibe um estrago diferente e permite outro.
///
/// Todas as checagens daqui sao LOCAIS e baratas: elas olham a historia e contam
/// violacoes, sem busca exponencial nenhuma. Elas nao substituem o juiz: elas
/// dizem QUAL propriedade quebrou quando ele diz que a historia nao e
/// linearizavel.
/// </summary>
public static class Garantias
{
    /// <summary>A escrita virtual que poe o valor inicial, antes de tudo.</summary>
    private static Operacao Inicial(int valor) => new(-1, Acao.Escrever, valor, 0, -2, -1);

    /// <summary>
    /// REGISTRADOR REGULAR: toda leitura devolve o valor da ultima escrita
    /// concluida, ou de alguma escrita concorrente com ela.
    ///
    /// Esta e exatamente a garantia que o quorum com R + W maior que N entrega, e
    /// ela e mais fraca que linearizabilidade de um jeito especifico: ela nao diz
    /// nada sobre DUAS leituras. Cada leitura, sozinha, devolve um valor
    /// defensavel; duas leituras seguidas podem devolver o novo e depois o velho.
    /// </summary>
    public static List<Operacao> LeiturasNaoRegulares(Historia historia, int valorInicial = 0)
    {
        ArgumentNullException.ThrowIfNull(historia);

        var escritas = historia.Operacoes.Where(o => o.Acao == Acao.Escrever).Append(Inicial(valorInicial)).ToList();
        var ruins = new List<Operacao>();

        foreach (var leitura in historia.Operacoes.Where(o => o.Acao == Acao.Ler))
        {
            // as escritas que poderiam explicar este valor
            var candidatas = escritas.Where(w => w.Argumento == leitura.Resposta).ToList();

            var alguma = candidatas.Any(w =>
                !Historia.AconteceuAntes(leitura, w) &&
                !escritas.Any(outra => Historia.AconteceuAntes(outra, leitura) && Historia.AconteceuAntes(w, outra)));

            if (!alguma) ruins.Add(leitura);
        }

        return ruins;
    }

    /// <summary>
    /// LEITURAS MONOTONICAS: dentro de um processo, uma leitura nunca anda para
    /// tras.
    ///
    /// E a garantia cuja ausencia produz a reclamacao mais comum de todas: a
    /// pagina mostra o dado novo, voce aperta atualizar e ela mostra o velho. Cada
    /// leitura, sozinha, estava certa.
    ///
    /// Aqui a ordem dos valores e a ordem em que eles foram escritos, e os valores
    /// sao escolhidos crescentes de proposito para a comparacao ser direta.
    /// </summary>
    public static int LeiturasQueAndaramParaTras(Historia historia)
    {
        ArgumentNullException.ThrowIfNull(historia);

        var quantas = 0;

        for (var p = 0; p < historia.Processos; p++)
        {
            var maior = int.MinValue;

            foreach (var operacao in historia.Do(p).Where(o => o.Acao == Acao.Ler))
            {
                if (maior != int.MinValue && operacao.Resposta < maior) quantas++;
                if (operacao.Resposta > maior) maior = operacao.Resposta;
            }
        }

        return quantas;
    }

    /// <summary>
    /// LEIA O QUE VOCE ESCREVEU: depois de escrever, o proprio processo nunca ve
    /// um valor mais velho que o que ele escreveu.
    ///
    /// E a garantia que o usuario cobra sem saber o nome: ele acabou de salvar,
    /// recarregou, e o que ele escreveu nao esta la. Para os outros usuarios,
    /// atraso e tolerado; para quem escreveu, parece perda de dado.
    /// </summary>
    public static int LeiturasQueNaoViramAPropriaEscrita(Historia historia)
    {
        ArgumentNullException.ThrowIfNull(historia);

        var quantas = 0;

        for (var p = 0; p < historia.Processos; p++)
        {
            var escreveu = int.MinValue;

            foreach (var operacao in historia.Do(p))
            {
                if (operacao.Acao == Acao.Escrever)
                {
                    if (operacao.Argumento > escreveu) escreveu = operacao.Argumento;
                    continue;
                }

                if (escreveu != int.MinValue && operacao.Resposta < escreveu) quantas++;
            }
        }

        return quantas;
    }

    /// <summary>
    /// A leitura devolveu um valor que NINGUEM ESCREVEU.
    ///
    /// Isto nao e consistencia fraca: e defeito. Consistencia fraca permite valor
    /// velho, nao permite valor inventado, e esta checagem separa as duas coisas.
    /// </summary>
    public static int LeiturasDeValorInventado(Historia historia, int valorInicial = 0)
    {
        ArgumentNullException.ThrowIfNull(historia);

        var escritos = historia.Operacoes
            .Where(o => o.Acao == Acao.Escrever)
            .Select(o => o.Argumento)
            .Append(valorInicial)
            .ToHashSet();

        return historia.Operacoes.Count(o => o.Acao == Acao.Ler && !escritos.Contains(o.Resposta));
    }

    /// <summary>
    /// O resumo de uma historia: o que ela cumpre e o que ela quebra.
    /// </summary>
    public sealed record Retrato(
        bool Linearizavel,
        bool SequencialmenteConsistente,
        int LeiturasNaoRegulares,
        int AndaramParaTras,
        int NaoViramAPropriaEscrita,
        int ValorInventado);

    public static Retrato Retratar(Historia historia, Especificacao especificacao, int limite = 20)
    {
        ArgumentNullException.ThrowIfNull(historia);

        return new Retrato(
            Linearizavel.Eh(historia, especificacao, limite),
            Sequencial.Eh(historia, especificacao, limite),
            LeiturasNaoRegulares(historia).Count,
            LeiturasQueAndaramParaTras(historia),
            LeiturasQueNaoViramAPropriaEscrita(historia),
            LeiturasDeValorInventado(historia));
    }
}
