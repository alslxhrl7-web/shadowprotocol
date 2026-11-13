namespace ShadowProtocol
{
    /// <summary>받침 여부로 한국어 조사를 고른다("을(를)" 같은 병기 대신). 예: Josa.Eul("덕트") → "덕트를"</summary>
    public static class Josa
    {
        /// <summary>마지막 글자에 받침이 있으면 true. 한글이 아니면 숫자 읽기·영문 끝소리로 추정.</summary>
        public static bool HasBatchim(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            int i = word.Length - 1;
            while (i > 0 && (char.IsWhiteSpace(word[i]) || word[i] == ')' || word[i] == '"' || word[i] == '\'' || word[i] == '.')) i--;
            char c = word[i];
            if (c >= '가' && c <= '힣') return (c - 0xAC00) % 28 != 0;
            if (c >= '0' && c <= '9') return "013678".IndexOf(c) >= 0;          // 영·일·삼·육·칠·팔
            char l = char.ToLowerInvariant(c);
            return l == 'l' || l == 'm' || l == 'n' || l == 'r';
        }

        public static string Eul(string w) => w + (HasBatchim(w) ? "을" : "를");
        public static string I(string w) => w + (HasBatchim(w) ? "이" : "가");
        public static string Eun(string w) => w + (HasBatchim(w) ? "은" : "는");
        public static string Gwa(string w) => w + (HasBatchim(w) ? "과" : "와");
    }
}
