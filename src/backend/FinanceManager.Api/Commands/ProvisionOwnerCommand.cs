using System.Text;
using FinanceManager.Application.Authentication;

namespace FinanceManager.Api.Commands;

internal static class ProvisionOwnerCommand
{
    public static async Task<int> RunAsync(OwnerAccessService access, CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Execute o provisionamento em um terminal interativo.");
            return 1;
        }
        Console.Write("Usuário: ");
        var username = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(username))
        {
            Console.Error.WriteLine("Informe um usuário válido.");
            return 1;
        }

        Console.Write("Senha: ");
        var password = new StringBuilder();
        for (var key = Console.ReadKey(intercept: true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(intercept: true))
        {
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
        Console.WriteLine();

        var result = await access.ProvisionAsync(username, password.ToString(), cancellationToken);
        password.Clear();
        switch (result)
        {
            case ProvisionOutcome.Created:
                Console.WriteLine("Titular provisionado.");
                return 0;
            case ProvisionOutcome.AlreadyProvisioned:
                Console.Error.WriteLine("Já existe um titular provisionado.");
                return 1;
            default:
                Console.Error.WriteLine("Confira o usuário e escolha uma senha de pelo menos 12 caracteres, com maiúscula, minúscula, número e símbolo.");
                return 1;
        }
    }
}
