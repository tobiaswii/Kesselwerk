using System; using System.Linq; using Kesselwerk.Core;
public static class TftSim {
  public static void Go(){
    for (int s=1;s<=12;s++){
      var g = TftGame.New("Tobias", CreatureDb.All.Where(d=>!d.isBoss).Select(d=>d.id), s);
      int rounds=0, evMax=0, evSum=0, fights=0;
      while(!g.Over && rounds<200){
        g.AutoTurn(g.players[0]);
        foreach(var (pr,res) in g.SimulateAll(null,false)){ if(res.events.Count>0){fights++; evSum+=res.events.Count; evMax=Math.Max(evMax,res.events.Count);} g.Apply(pr,res);} 
        g.EndRound(); rounds++;
      }
      var w=g.players.First(p=>p.Alive);
      Console.WriteLine($"Seed {s}: {rounds} Runden, Ende {g.RoundName}, Sieger {w.name} (Stufe {w.level}, {w.BoardCount} Monster, Sterne {string.Join(",",w.board.Where(u=>u!=null).Select(u=>u.star))}), Ereignisse Ø {evSum/Math.Max(1,fights)} max {evMax}; Plätze {string.Join(" ",g.players.Select(p=>p.name+":"+p.place))}");
    }
  }
}
