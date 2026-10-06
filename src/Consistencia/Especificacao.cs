namespace Conde.Consistencia;

/// <summary>
/// A ESPECIFICACAO SEQUENCIAL: o que o objeto faz quando so uma pessoa mexe nele
/// por vez.
///
/// Esta e a parte que quase todo mundo pula, e ela e metade da definicao. Dizer
/// "o sistema e linearizavel" sem dizer linearizavel em relacao a QUE nao
/// significa nada: linearizabilidade e "existe uma ordem total das operacoes,
/// compativel com o tempo real, que a ESPECIFICACAO SEQUENCIAL aceita".
///
/// Sem a especificacao, nao ha o que aceitar.
/// </summary>
public abstract record Especificacao(string Nome)
{
    public abstract int Inicial { get; }

    /// <summary>
    /// O objeto aceita esta operacao neste estado, e qual estado fica depois.
    ///
    /// A resposta ja esta na operacao: o cliente JA recebeu aquele valor. A
    /// pergunta nao e "o que o objeto devolveria", e sim "o objeto PODERIA ter
    /// devolvido isso daqui". E a diferenca que faz a checagem ser uma busca e
    /// nao uma simulacao.
    /// </summary>
    public abstract bool Aceita(int estado, Operacao operacao, out int novo);

    public sealed override string ToString() => Nome;

    /// <summary>
    /// O REGISTRADOR: guarda um valor, a leitura devolve o ultimo escrito.
    ///
    /// E o objeto mais simples que existe e e o que basta para mostrar tudo que
    /// este repositorio mostra. Problema de consistencia nao precisa de estrutura
    /// complicada: ele aparece inteiro numa variavel.
    /// </summary>
    public static Especificacao Registrador(int inicial = 0) => new ComoRegistrador(inicial);

    /// <summary>
    /// O CONTADOR: escrever soma, ler devolve a soma.
    ///
    /// Ele existe para mostrar o contrario do registrador: um objeto em que a
    /// ordem das escritas NAO importa para o valor final, mas importa para as
    /// leituras pelo caminho.
    /// </summary>
    public static Especificacao Contador(int inicial = 0) => new ComoContador(inicial);

    private sealed record ComoRegistrador(int Partida) : Especificacao("registrador")
    {
        public override int Inicial => Partida;

        public override bool Aceita(int estado, Operacao operacao, out int novo)
        {
            ArgumentNullException.ThrowIfNull(operacao);

            if (operacao.Acao == Acao.Escrever)
            {
                novo = operacao.Argumento;
                return true;
            }

            novo = estado;
            return operacao.Resposta == estado;
        }
    }

    private sealed record ComoContador(int Partida) : Especificacao("contador")
    {
        public override int Inicial => Partida;

        public override bool Aceita(int estado, Operacao operacao, out int novo)
        {
            ArgumentNullException.ThrowIfNull(operacao);

            if (operacao.Acao == Acao.Escrever)
            {
                novo = estado + operacao.Argumento;
                return true;
            }

            novo = estado;
            return operacao.Resposta == estado;
        }
    }
}
