using System;
using System.Collections.Generic;

namespace TDS.Core
{
    /// <summary>
    /// 개발용 콘솔의 명령 레지스트리/파서(순수, 테스트 가능). 이름으로 명령을 등록하고,
    /// "cmd arg1 arg2" 입력을 파싱해 디스패치하고 출력 문자열을 돌려준다. 입력창/토글 등 UI는
    /// 글루(MonoBehaviour)가 이 위에 얹는다. UnityEngine 의존 없음.
    /// </summary>
    public class ConsoleRegistry
    {
        public sealed class Command
        {
            public string Name;
            public string Help;
            public Func<string[], string> Run;
        }

        private readonly Dictionary<string, Command> commands =
            new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<Command> Commands => commands.Values;
        public int Count => commands.Count;

        /// <summary>명령 등록(같은 이름이면 덮어씀). 대소문자 무시.</summary>
        public void Register(string name, string help, Func<string[], string> run)
        {
            if (string.IsNullOrWhiteSpace(name) || run == null) return;
            commands[name.Trim()] = new Command { Name = name.Trim(), Help = help ?? "", Run = run };
        }

        public bool Has(string name) => !string.IsNullOrEmpty(name) && commands.ContainsKey(name);

        /// <summary>입력을 명령 + 인자로 분리(공백 기준, 빈 토큰 제거).</summary>
        public static (string cmd, string[] args) Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return ("", Array.Empty<string>());
            var parts = input.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0];
            string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            return (cmd, args);
        }

        /// <summary>입력 실행 → 출력 문자열. 빈 입력은 빈 문자열, 미등록은 안내, 예외는 메시지로.</summary>
        public string Execute(string input)
        {
            var (cmd, args) = Parse(input);
            if (cmd.Length == 0) return "";
            if (!commands.TryGetValue(cmd, out var c))
                return $"unknown command: {cmd}  (type 'help')";
            try { return c.Run(args) ?? ""; }
            catch (Exception e) { return $"error: {e.Message}"; }
        }
    }
}
