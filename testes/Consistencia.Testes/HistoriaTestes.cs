using Conde.Consistencia;
using Xunit;

namespace Conde.Consistencia.Testes;

public class HistoriaTestes
{
    [Fact]
    public void OSorteioEhDeterminista()
    {
        var a = new Sorteio(7);
        var b = new Sorteio(7);

        for (var i = 0; i < 1000; i++) Assert.Equal(a.Proximo(), b.Proximo());
    }

    /// <summary>
    /// A fracao precisa cobrir os DOIS LADOS do meio: um deslocamento de 33 em vez
    /// de 32 deixaria o gerador com 31 bits, e "escreve com probabilidade 0,6"
    /// viraria "escreve sempre". Os numeros continuariam plausiveis.
    /// </summary>
    [Fact]
    public void AFracaoCobreOsDoisLadosDoMeio()
    {
        var sorteio = new Sorteio(7);
        var acima = 0;
        var maior = 0.0;

        for (var i = 0; i < 10_000; i++)
        {
            var f = sorteio.Fracao();

            Assert.InRange(f, 0.0, 1.0);
            if (f > 0.5) acima++;
            if (f > maior) maior = f;
        }

        Assert.InRange(acima, 4_700, 5_300);
        Assert.True(maior > 0.999, $"o maior sorteio foi {maior}");
    }

    [Fact]
    public void EscolherDaItensDistintosDentroDoLimite()
    {
        var sorteio = new Sorteio(3);

        for (var i = 0; i < 1_000; i++)
        {
            var escolhidos = sorteio.Escolher(3, 5);

            Assert.Equal(3, escolhidos.Length);
            Assert.Equal(3, escolhidos.Distinct().Count());
            Assert.All(escolhidos, e => Assert.InRange(e, 0, 4));
        }
    }

    [Fact]
    public void EscolherTodosDaUmaPermutacao()
    {
        var sorteio = new Sorteio(3);
        var escolhidos = sorteio.Escolher(5, 5);

        Assert.Equal(Enumerable.Range(0, 5), escolhidos.OrderBy(x => x));
    }

    /// <summary>
    /// O INTERVALO e fechado no comeco e ABERTO no fim: uma operacao que termina
    /// no instante 5 e outra que comeca no 5 nao se cruzam.
    ///
    /// Com o "menor" estrito, duas operacoes seguidas do mesmo processo apareciam
    /// como concorrentes e a busca aceitava ordens que invertem o programa do
    /// cliente. O resultado era uma coisa que nao pode existir: historia
    /// linearizavel e nao sequencialmente consistente.
    /// </summary>
    [Fact]
    public void OIntervaloEhFechadoNoComecoEAbertoNoFim()
    {
        var antes = new Operacao(0, Acao.Escrever, 1, 0, 0, 5);
        var depois = new Operacao(0, Acao.Ler, 0, 1, 5, 9);

        Assert.True(Historia.AconteceuAntes(antes, depois));
        Assert.False(Historia.AconteceuAntes(depois, antes));
        Assert.False(Historia.Concorrentes(antes, depois));

        var cruzando = new Operacao(1, Acao.Ler, 0, 0, 4, 9);

        Assert.True(Historia.Concorrentes(antes, cruzando));
        Assert.False(Historia.AconteceuAntes(antes, cruzando));
    }

    [Fact]
    public void AHistoriaSequencialNaoTemNadaConcorrente()
    {
        var historia = Historia.Sequencial(
        [
            (0, Acao.Escrever, 1, 0),
            (1, Acao.Ler, 0, 1),
            (0, Acao.Ler, 0, 1),
        ]);

        Assert.Equal(3, historia.Quantas);
        Assert.Equal(0, historia.QuantosParesConcorrentes());
        Assert.Equal(2, historia.Processos);
    }

    [Fact]
    public void AsOperacoesDeUmProcessoSaemNaOrdemEmQueEleAsPediu()
    {
        var historia = Historia.Sequencial(
        [
            (0, Acao.Escrever, 1, 0),
            (1, Acao.Escrever, 2, 0),
            (0, Acao.Ler, 0, 2),
            (0, Acao.Escrever, 3, 0),
        ]);

        var doZero = historia.Do(0);

        Assert.Equal(3, doZero.Count);
        Assert.Equal(1, doZero[0].Argumento);
        Assert.Equal(Acao.Ler, doZero[1].Acao);
        Assert.Equal(3, doZero[2].Argumento);

        for (var i = 1; i < doZero.Count; i++)
            Assert.True(doZero[i - 1].Comeco < doZero[i].Comeco);
    }

    [Fact]
    public void UmProcessoSoNuncaTemOperacaoConcorrente()
    {
        for (ulong semente = 1; semente <= 50; semente++)
        {
            var pedidos = Execucao.Sortear(1, 6, 0.4, semente);
            var historia = Execucao.Rodar(pedidos, 3, 2, 2, semente);

            Assert.Equal(0, historia.QuantosParesConcorrentes());
        }
    }

    [Fact]
    public void AHistoriaVaziaNaoTemNada()
    {
        var historia = new Historia([]);

        Assert.Equal(0, historia.Quantas);
        Assert.Equal(0, historia.Processos);
        Assert.Equal(0, historia.QuantosParesConcorrentes());
    }
}
