using Conde.Consistencia;
using Xunit;

namespace Conde.Consistencia.Testes;

public class SequencialTestes
{
    private static readonly Especificacao Registrador = Especificacao.Registrador();

    /// <summary>
    /// A HISTORIA QUE SEPARA AS DUAS DEFINICOES, e que e o sistema que todo mundo
    /// ja usou: voce salva, avisa o colega, ele recarrega e ve o valor velho.
    ///
    /// Nao e linearizavel, e e sequencialmente consistente. Nenhuma regra foi
    /// quebrada: a ordem do processo que leu nao exige nada sobre o que o outro
    /// processo fez antes.
    /// </summary>
    [Fact]
    public void AQueSeparaAsDuasEhSequencialENaoLinearizavel()
    {
        var historia = Sequencial.AQueSeparaAsDuas();

        Assert.False(Linearizavel.Eh(historia, Registrador));
        Assert.True(Sequencial.Eh(historia, Registrador));

        var ordem = Sequencial.Ordem(historia, Registrador)!;

        Assert.Equal(Acao.Ler, ordem[0].Acao);
        Assert.Equal(Acao.Escrever, ordem[1].Acao);
    }

    /// <summary>
    /// Consistencia fraca permite valor VELHO, nao permite valor INVENTADO. E a
    /// linha entre "mais fraco" e "errado".
    /// </summary>
    [Fact]
    public void ValorInventadoNaoEhNenhumaDasDuas()
    {
        var historia = Sequencial.AQueNaoEhNenhumaDasDuas();

        Assert.False(Linearizavel.Eh(historia, Registrador));
        Assert.False(Sequencial.Eh(historia, Registrador));
        Assert.Equal(1, Garantias.LeiturasDeValorInventado(historia));
    }

    /// <summary>
    /// A ORDEM do programa de cada processo e obrigatoria mesmo na definicao
    /// fraca: um processo que escreve e depois le nao pode ler o valor de antes.
    /// </summary>
    [Fact]
    public void AOrdemDoProprioProcessoEhObrigatoriaAteNaDefinicaoFraca()
    {
        var historia = Historia.Sequencial(
        [
            (0, Acao.Escrever, 1, 0),
            (0, Acao.Ler, 0, 0),
        ]);

        Assert.False(Sequencial.Eh(historia, Registrador));
        Assert.False(Linearizavel.Eh(historia, Registrador));
    }

    /// <summary>
    /// A ordem devolvida e conferida sem a busca: ela respeita o programa de cada
    /// processo e a especificacao aceita cada passo.
    /// </summary>
    [Fact]
    public void AOrdemDevolvidaEhConferidaSemAbusca()
    {
        var conferidas = 0;

        for (ulong semente = 1; semente <= 200; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
            var historia = Execucao.Rodar(pedidos, 3, 1, 1, semente * 7 + 1);

            var ordem = Sequencial.Ordem(historia, Registrador);
            if (ordem is null) continue;

            conferidas++;

            Assert.Equal(historia.Quantas, ordem.Count);
            Assert.True(Linearizavel.OrdemPresta(ordem, Registrador), $"semente {semente}");
            Assert.True(Sequencial.OrdemRespeitaOsProcessos(ordem), $"semente {semente}");
        }

        Assert.True(conferidas > 30, $"so {conferidas} historias sequenciais para conferir");
    }

    /// <summary>
    /// E a ordem da definicao fraca NEM SEMPRE respeita o relogio: quando ela
    /// respeita, a historia tambem era linearizavel.
    /// </summary>
    [Fact]
    public void AOrdemFracaNemSempreRespeitaORelogio()
    {
        var violou = 0;

        for (ulong semente = 1; semente <= 200; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
            var historia = Execucao.Rodar(pedidos, 3, 1, 1, semente * 7 + 1);

            var ordem = Sequencial.Ordem(historia, Registrador);
            if (ordem is null) continue;

            if (!Linearizavel.OrdemRespeitaORelogio(ordem)) violou++;
        }

        Assert.True(violou > 0, "nenhuma ordem fraca precisou desrespeitar o relogio");
    }

    [Fact]
    public void HistoriaVaziaEhSequencial()
    {
        Assert.True(Sequencial.Eh(new Historia([]), Registrador));
    }

    [Fact]
    public void HistoriaGrandeDemaisEhRecusada()
    {
        var pedidos = Execucao.Sortear(4, 8, 0.4, 1);
        var historia = Execucao.Rodar(pedidos, 3, 2, 2, 1);

        Assert.Throws<ArgumentException>(() => Sequencial.Eh(historia, Registrador));
    }
}
