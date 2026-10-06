using Conde.Consistencia;
using Xunit;

namespace Conde.Consistencia.Testes;

public class EspecificacaoTestes
{
    [Fact]
    public void ORegistradorDevolveOUltimoEscrito()
    {
        var registrador = Especificacao.Registrador();
        var estado = registrador.Inicial;

        Assert.Equal(0, estado);

        Assert.True(registrador.Aceita(estado, new Operacao(0, Acao.Ler, 0, 0, 0, 1), out _));
        Assert.False(registrador.Aceita(estado, new Operacao(0, Acao.Ler, 0, 5, 0, 1), out _));

        Assert.True(registrador.Aceita(estado, new Operacao(0, Acao.Escrever, 5, 0, 0, 1), out estado));
        Assert.Equal(5, estado);

        Assert.True(registrador.Aceita(estado, new Operacao(0, Acao.Ler, 0, 5, 0, 1), out _));
        Assert.False(registrador.Aceita(estado, new Operacao(0, Acao.Ler, 0, 0, 0, 1), out _));
    }

    /// <summary>
    /// A ESCRITA sempre e aceita, em qualquer estado: ela nao devolve nada para
    /// conferir. O que a especificacao julga e a LEITURA.
    /// </summary>
    [Fact]
    public void AEscritaEhSempreAceita()
    {
        var registrador = Especificacao.Registrador();

        foreach (var estado in new[] { 0, 1, 99, -3 })
            Assert.True(registrador.Aceita(estado, new Operacao(0, Acao.Escrever, 7, 0, 0, 1), out var novo) && novo == 7);
    }

    [Fact]
    public void OContadorSomaEmVezDeTrocar()
    {
        var contador = Especificacao.Contador();
        var estado = contador.Inicial;

        Assert.True(contador.Aceita(estado, new Operacao(0, Acao.Escrever, 3, 0, 0, 1), out estado));
        Assert.Equal(3, estado);

        Assert.True(contador.Aceita(estado, new Operacao(0, Acao.Escrever, 4, 0, 0, 1), out estado));
        Assert.Equal(7, estado);

        Assert.True(contador.Aceita(estado, new Operacao(0, Acao.Ler, 0, 7, 0, 1), out _));
        Assert.False(contador.Aceita(estado, new Operacao(0, Acao.Ler, 0, 4, 0, 1), out _));
    }

    /// <summary>
    /// O ESTADO INICIAL e parte da especificacao, e trocar ele muda quais
    /// historias sao validas. Sem ele declarado, "a leitura devolveu zero" nao tem
    /// como ser julgada.
    /// </summary>
    [Fact]
    public void OEstadoInicialFazParteDaEspecificacao()
    {
        var doZero = Especificacao.Registrador();
        var doCinco = Especificacao.Registrador(5);

        var leuZero = Historia.Sequencial([(0, Acao.Ler, 0, 0)]);

        Assert.True(Linearizavel.Eh(leuZero, doZero));
        Assert.False(Linearizavel.Eh(leuZero, doCinco));
    }

    /// <summary>
    /// A MESMA historia pode ser valida para um objeto e invalida para outro. E
    /// por isso que "o sistema e linearizavel" sem dizer em relacao a que nao
    /// significa nada.
    /// </summary>
    [Fact]
    public void AMesmaHistoriaMudaDeVeredictoComAEspecificacao()
    {
        var historia = Historia.Sequencial(
        [
            (0, Acao.Escrever, 3, 0),
            (0, Acao.Escrever, 4, 0),
            (0, Acao.Ler, 0, 7),
        ]);

        Assert.False(Linearizavel.Eh(historia, Especificacao.Registrador()));
        Assert.True(Linearizavel.Eh(historia, Especificacao.Contador()));
    }

    [Fact]
    public void CadaEspecificacaoTemNomeESeImprimePeloNome()
    {
        foreach (var especificacao in new[] { Especificacao.Registrador(), Especificacao.Contador() })
        {
            Assert.False(string.IsNullOrWhiteSpace(especificacao.Nome));
            Assert.Equal(especificacao.Nome, especificacao.ToString());
        }
    }
}
