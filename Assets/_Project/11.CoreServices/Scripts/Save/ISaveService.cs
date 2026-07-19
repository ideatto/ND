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
 *
 * Usage for Team Members
 * - FrameworkRoot에서 생성한 구현체를 통해 접근하고, 호출자는 반환된 SaveData의 null 가능성과 버전 정책을 구현체 문서에 따라 처리한다.
 *
 * Main Public APIs
 * - HasSaveData(): 저장 파일 존재 여부를 확인한다.
 * - CreateNewGameData(): 기본값으로 초기화된 새 저장 데이터를 만든다.
 * - Load(): 저장 데이터를 로드하거나 fallback 데이터를 반환한다.
 * - Save(...): 현재 저장 데이터를 영속화한다.
 * - ResetSaveData(): 저장 데이터를 삭제한다.
 *
 * Important Notes
 * - 이 interface는 저장 위치와 직렬화 형식을 고정하지 않는다.
 * - SaveData 참조를 전달받는 구현체는 호출자와 같은 객체 그래프를 다룰 수 있다.
 */
namespace ND.Framework
{
    /// <summary>Identifies the stage at which a save operation failed.</summary>
    public enum SaveFailureReason
    {
        None,
        InvalidData,
        SerializationFailed,
        WriteFailed,
        Unknown
    }

    /// <summary>Reports whether a save completed and, on failure, why it did not.</summary>
    public sealed class SaveResult
    {
        private SaveResult(bool succeeded, SaveFailureReason failureReason, string message, string failedDataCategory)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Message = message ?? string.Empty;
            FailedDataCategory = failedDataCategory;
        }

        /// <summary>True only after the save file write completes without an exception.</summary>
        public bool Succeeded { get; }

        /// <summary>The stable failure classification, or None for a successful save.</summary>
        public SaveFailureReason FailureReason { get; }

        /// <summary>Developer diagnostic text. This is not player-facing UI copy.</summary>
        public string Message { get; }

        /// <summary>The associated data category, or null when no category is known.</summary>
        public string FailedDataCategory { get; }

        public static SaveResult Success()
        {
            return new SaveResult(true, SaveFailureReason.None, string.Empty, null);
        }

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
    /// 구현체는 실패 시 예외를 외부로 전파할지, 새 저장 데이터로 복구할지 자체 정책을 문서화해야 한다.
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
        /// <returns>파일 쓰기 완료 여부와 실패 단계 및 개발자 진단 정보를 담은 결과.</returns>
        SaveResult Save(SaveData data);

        /// <summary>
        /// 현재 저장 데이터를 삭제하거나 초기 상태로 되돌린다.
        /// </summary>
        void ResetSaveData();
    }
}
