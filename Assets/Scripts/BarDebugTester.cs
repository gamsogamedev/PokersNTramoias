using UnityEngine;

/// <summary>
/// Script temporário só pra testar as barras. Pode apagar depois que o
/// sistema de trapaça e o motor de poker estiverem prontos.
///
/// Teclas:
///   S = +15 de desconfiança (simula uma trapaça)
///   D = -15 de desconfiança
///   M = move 100 fichas dos oponentes para o comparsa
///   R = reinicia a desconfiança
/// </summary>
public class BarDebugTester : MonoBehaviour
{
    public SuspicionSystem suspicion;
    public MoneyTracker money;

    public float suspicionStep = 15f;
    public int moneyStep = 100;

    private void Update()
    {
        if (suspicion != null)
        {
            if (Input.GetKeyDown(KeyCode.S)) suspicion.AddSuspicion(suspicionStep);
            if (Input.GetKeyDown(KeyCode.D)) suspicion.ReduceSuspicion(suspicionStep);
            if (Input.GetKeyDown(KeyCode.R)) suspicion.ResetSuspicion();
        }

        if (money != null && Input.GetKeyDown(KeyCode.M))
        {
            money.MoveToAccomplice(moneyStep);
        }
    }

    // Exemplos de métodos que você pode ligar nos eventos do SuspicionSystem no Inspector
    public void LogAlert()  => Debug.Log("Os jogadores estão ficando desconfiados...");
    public void LogDanger() => Debug.Log("PERIGO: eles estão te observando de perto!");
    public void LogBusted() => Debug.Log("Você foi descoberto!");
    public void LogGoal()   => Debug.Log("Seu comparsa ficou com todo o dinheiro. Vitória!");
}
