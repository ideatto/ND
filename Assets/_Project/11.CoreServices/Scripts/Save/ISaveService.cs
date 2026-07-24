/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices 저장 시스템이 제공해야 하는 save-data 접근 계약을 정의한다.
 * - FrameworkRoot와 gameplay service가 구체적인 저장 구현에 직접 의존하지 않도록 한다.
 *
 * Main Features
 * - 저장 데이터 존재 확인, 새 게임 데이터 생성, 로드, 저장, 초기화를 위한 API를 제공한다.
 * - Save(...)는 SaveResult로 저장 성공 여부와 실패 단계를 반환한다.
 *
 * Usage for Team Members
 * - FrameworkRoot에서 생성한 구현체를 통해 접근한다.
 * - Load() 반환 SaveData의 null 가능성과 version 복구 정책은 구현체 문서를 따른다.
 * - 중요 저장 흐름은 SaveResult.Succeeded를 검사해 디스크 기록 실패를 구분한다.
 * - SaveResult.Message는 개발자 진단용이며 플레이어 UI 문구로 사용하지 않는다.
 *
 * Main Public APIs
 * - HasSaveData(): 저장 파일 존재 여부를 확인한다.
 * - CreateNewGameData(): 기본값으로 초기화된 새 저장 데이터를 만든다.
 * - Load(): 저장 데이터를 로드하거나 fallback 데이터를 반환한다.
 * - Save(...): SaveData를 영속화하고 SaveResult로 결과를 반환한다.
 * - ResetSaveData(): 저장 데이터를 삭제한다.
 * - SaveFailureReason / SaveResult: 저장 실패 분류와 결과 계약을 제공한다.
 *
 * Important Notes
 * - 이 interface는 저장 위치와 직렬화 형식을 고정하지 않는다.
 * - SaveData 참조를 전달받는 구현체는 호출자와 같은 객체 그래프를 다룰 수 있다.
 * - SaveResult 검사는 호출자 책임이며, 반환값을 무시해도 컴파일은 가능하다.
 *
 * Related Documentation
 * - Docs/Personal_Documents/CSU/SaveDataPolicy/Save_Result_API_Implementation_Logic.md
 */
namespace ND.Framework
{
    /// <summary>
    /// 저장 작업이 실패한 단계를 구분한다.
    /// </summary>
    public enum SaveFailureReason
    {
        /// <summary>저장 성공 또는 실패 사유 없음.</summary>
        None,

        /// <summary>입력 SaveData가 null이거나 저장에 사용할 수 없음.</summary>
        InvalidData,

        /// <summary>직렬화 단계(JsonUtility 등)에서 실패.</summary>
        SerializationFailed,

        /// <summary>영속 저장소 쓰기 단계에서 실패.</summary>
        WriteFailed,

        /// <summary>정규화, 메타데이터 갱신 등 기타 예외.</summary>
        Unknown
    }

    /// <summary>
    /// Save(...) 호출의 성공 여부와 실패 진단 정보를 담는다.
    /// </summary>
    /// <remarks>
    /// Succeeded는 구현체가 파일 쓰기까지 완료했을 때만 true이다.
    /// Message는 로그용이며 플레이어 UI 문구로 사용하지 않는다.
    /// </remarks>
    public sealed class SaveResult
    {
        private SaveResult(bool succeeded, SaveFailureReason failureReason, string message, string failedDataCategory)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Message = message ?? string.Empty;
            FailedDataCategory = failedDataCategory;
        }

        /// <summary>
        /// 영속 저장소 쓰기가 예외 없이 완료되면 true.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 실패 분류. 성공이면 None.
        /// </summary>
        public SaveFailureReason FailureReason { get; }

        /// <summary>
        /// 개발자 진단용 텍스트. 플레이어 UI 문구가 아니다.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 실패와 연관된 데이터 범주. 알 수 없으면 null.
        /// </summary>
        public string FailedDataCategory { get; }

        /// <summary>
        /// 저장이 완료된 성공 결과를 만든다.
        /// </summary>
        public static SaveResult Success()
        {
            return new SaveResult(true, SaveFailureReason.None, string.Empty, null);
        }

        /// <summary>
        /// 저장 실패 결과를 만든다.
        /// </summary>
        /// <param name="failureReason">None이면 Unknown으로 보정된다.</param>
        /// <param name="message">개발자 진단용 실패 메시지.</param>
        /// <param name="failedDataCategory">실패와 연관된 데이터 범주. 없으면 null.</param>
        /// <returns>Succeeded가 false인 SaveResult.</returns>
        public static SaveResult Failure(
            SaveFailureReason failureReason,
            string message,
            string failedDataCategory = null)
        {
            if (failureReason == SaveFailureReason.None)
            {
                failureReason = SaveFailureReason.Unknown;
            }

            return new SaveResult(false, failureReason, message, failedDataCategory);
        }
    }

    /// <summary>
    /// 저장 데이터 생성, 로드, 저장, 삭제를 담당하는 서비스 계약이다.
    /// </summary>
    /// <remarks>
    /// Load()는 구현체 복구 정책에 따라 fallback SaveData를 반환할 수 있다.
    /// Save()는 SaveResult로 성공 여부를 반환하며, 구현체는 예외 전파 여부를 문서화해야 한다.
    /// </remarks>
    public interface ISaveService
    {
        /// <summary>
        /// 현재 저장 위치에 로드 가능한 저장 데이터가 있는지 확인한다.
        /// </summary>
        /// <returns>저장 데이터가 존재하면 true, 없으면 false.</returns>
        bool HasSaveData();

        /// <summary>
        /// 새 게임 시작에 사용할 기본 저장 데이터를 생성한다.
        /// </summary>
        /// <returns>현재 저장 schema version과 기본값이 반영된 SaveData.</returns>
        SaveData CreateNewGameData();

        /// <summary>
        /// 저장 데이터를 로드한다.
        /// </summary>
        /// <returns>로드된 저장 데이터 또는 구현체의 복구 정책에 따라 생성된 기본 저장 데이터.</returns>
        SaveData Load();

        /// <summary>
        /// 전달된 저장 데이터를 영속 저장소에 기록한다.
        /// </summary>
        /// <param name="data">저장할 SaveData 인스턴스. null이면 InvalidData 실패 결과를 반환할 수 있다.</param>
        /// <returns>
        /// 영속 저장소 쓰기가 완료되면 Succeeded가 true인 SaveResult.
        /// null 입력, 직렬화 실패, 쓰기 실패 등은 FailureReason으로 구분된 SaveResult.
        /// </returns>
        /// <remarks>
        /// 호출자는 중요 저장 흐름에서 Succeeded를 검사해야 한다.
        /// 반환값을 무시해도 컴파일은 가능하지만, 저장 실패를 성공으로 처리하지 않도록 주의한다.
        /// </remarks>
        SaveResult Save(SaveData data);

        /// <summary>
        /// 현재 저장 데이터를 삭제하거나 초기 상태로 되돌린다.
        /// </summary>
        void ResetSaveData();
    }
}
