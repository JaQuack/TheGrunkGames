using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TheGrunkGames2.Services;
using System.Collections.Generic;
using TheGrunkGames2.Objects;
using System;
using Newtonsoft.Json;

namespace TheGrunkGames2.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StatController : Controller
    {
        private readonly ILogger<StatController> _logger;
        private readonly GameService _gameService;

        public StatController(ILogger<StatController> logger, GameService gameService)
        {
            _logger = logger;
            _gameService = gameService;
        }

        [HttpGet]
        public string RunTestGame(int nrTeams = 8, int rounds = 10)
        {
            var tournament = GetDummyTournament(nrTeams);
            _gameService.SetTournament(tournament);

            for (int i = 0; i < rounds; i++)
            {
                //var round = _gameService.GetNextRound();
                var round = _gameService.GetNextRoundNewLogic();
                foreach (var match in round.Matches)
                {
                    var matchResult = GetRandomMatchResult(match.MatchId);
                    _gameService.CompleteMatch(matchResult);
                }
            }
            return JsonConvert.SerializeObject(new
            {
                Standings = _gameService.GetTeamStandings(),
                TeamStats = _gameService.GetTeamStats(),
                RoundSelectStats = _gameService.GetRoundStats()
            });
        }

        private Tournament GetDummyTournament(int nrTeams)
        {
            var games = new List<Game>
            {
                new Game { Name = "Mario Kart 64", Device = Device.TV },
                new Game { Name = "Age of Empires 2", Device = Device.PC_Steam },
                new Game { Name = "NFS: Underground 2", Device = Device.PC_Steam},
                new Game { Name = "Half life 1", Device = Device.PC_Steam_2 },
                new Game { Name = "Audiosurf", Device = Device.PC_Couch },
                new Game { Name = "Doom 2", Device = Device.PC_Couch },
                new Game { Name = "Fifa 98", Device = Device.TV },
                new Game { Name = "Tetris Party Deluxe", Device = Device.TV_Wii },
                new Game { Name = "Dark Messiah of Might and Magic", Device = Device.PC_Steam_2 },
                new Game { Name = "Mario Tennis", Device = Device.TV_Wii },
                new Game { Name = "Beerpong", Device = Device.IRL },
                new Game { Name = "TIMETRIAL", Device = Device.TIMETRIAL}
            };
            var teams = new List<Team>();
            for (int i = 0; i < nrTeams; i++)
            {
                teams.Add(new Team { TeamName = $"Team_{i}" });
            }
            return new Tournament { Games = games, Teams = teams, Rounds = new List<Round>() };
        }

        private MatchResult GetRandomMatchResult(int matchId)
        {
            var rnd = new Random();
            var nr = rnd.Next(11) + 1;
            var matchResult = new MatchResult { MatchId = matchId };

            if (nr < 6)
            {
                matchResult.Team1Score = 3;
            }
            else if (nr < 11)
            {
                matchResult.Team2Score = 3;
            }
            else
            {
                matchResult.Team1Score = 1;
                matchResult.Team2Score = 1;
            }
            return matchResult;
        }
    }
}
