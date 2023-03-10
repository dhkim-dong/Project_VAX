using Photon.Pun.Demo.Cockpit;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.Analytics.Internal;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// MainMenuScene 타이틀 비디오 영상 제작을 위한
/// VAX 게임 대략적이 자동 플레이
/// </summary>
public class VAXAutoPlayManager : MonoBehaviour
{
    #region Enum

    /// <summary>
    /// VAX 자동 플레이 진행 상태
    /// </summary>
    private enum VaxAutoPlayState
    {
        Init,                   // 전체 초기화 작업
        START,                  // 교실에 플레이어 입장과 일반 좀비 등장
        NORMAL_ZOMBIES_DEAD,    // 일반 좀비가 모두 죽으면 마피아 역할인 Girl2가 Man1 공격
        MAN1_DEAD,              // Man1이 죽으면 생존자 역할인 Girl1과 Man2가 마피아(Girl2) 공격
        MAFIA_DEAD,             // 마피아 역할인 Girl2가 죽으면 보스 좀비 등장
        BOSS_ZOMBIE_DEAD,       // 보스 좀비가 죽으면 열쇠 획득
        FIND_VAX,               // 병원으로 플레이어 위치 이동, 문열고 들어감 백신 찾음
        RESCUE_REQEUST,         // 운동장으로 위치 이동, Girl1 신호탄 발사
        FINAL_ZOMBIE,           // 일반 좀비 웨이브 발생 플레이어는 좀비 공격
        MAN2_DEAD,              // Man2가 죽으면 헬기 도착
        RESCUE_SUCCESS,         // Girl1가 헬기로 이동 후 탈출
        END                     // VAX UI 뜨고 종료
    }

    /// <summary>
    /// 플레이어 상태 - 애니메이션에 반영
    /// </summary>
    private enum PlayerState
    {
        IDLE,       // 기본
        WALK,       // 걷기
        RUN,        // 뛰기
        ATTACK,     // 공격
        DAMAGE,     // 데미지 입음
        SURPRISED,  // 놀람
        THINKING,   // 생각중
        JOY,        // 기쁨
        DIE         // 죽음
    }

    /// <summary>
    /// 좀비 상태 - 애니메이션에 반영
    /// </summary>
    private enum ZombieState
    {
        IDLE,       // 기본
        WALK,       // 걷기
        RUN,        // 뛰기
        ATTACK,     // 공격
        DAMAGE,     // 데미지 입음
        DIE         // 죽음
    }

    #endregion Enum

    #region Variable

    private VaxAutoPlayState vaxAutoPlayState;                      // VAX 자동 플레이 진행 상태

    [SerializeField] private GameObject mainCamera;                 // 메인카메라 오브젝트
    [SerializeField] private Transform[] mainCameraPos;             // 메인카메라 위치[교실, 병원, 운동장]

    [SerializeField] private float runSpeed = 10f;

    [SerializeField] private GameObject hospitalKey;                // 보스 몬스터를 죽이면 나오는 병원 키
    [SerializeField] private Transform hospitalKeyPos;              // 카메라 병원 이동후 이동할 병원 키 위치
    [SerializeField] private Animator hospitalDoorAnim;             // 병원 문 애니메이션
    [SerializeField] private GameObject vax;                        // 백신 오브젝트
    [SerializeField] private GameObject firework;                   // 신호탄 오브젝트

    private bool isPlayerInit;                                      // 플레이어 초기화 함수 완료 여부

    private PlayerState[] playersState;                             // 플레이어 상태
    [SerializeField] private GameObject[] classPlayers;             // 생존자3, 마피아1
    [SerializeField] private Transform[] playersClassPos;           // 플레이어 교실 위치
    [SerializeField] private GameObject[] classPlayerParentBloods;  // 교실 플레이어 피 파티클 부모 객체
    private GameObject[] classPlayerBloods;           // 교실 플레이어 피 파티클(Man1, Girl1, Girl2)
    private NavMeshAgent[] classPlayerAgent;                        // 플레이어 경로계산 AI 에이전트
    private Animator[] classPlayersAnimator;                        // 플레이어 애니메이터 컴포넌트
    [SerializeField] private ParticleSystem[] classPlayerRifles;    // 플레이어 총 파티클
    private bool isClassPlayerInit;                                 // 교실 플레이어 초기화 여부

    [SerializeField] private GameObject[] hospitalPlayers;          // Man2(생존자), Girl1(생존자)
    [SerializeField] private Transform[] playersHospitalPos;        // 플레이어 병원 위치[Man2StartPos, Girl2StartPos, Man2TargetPos, Girl2TargetPos]
    private NavMeshAgent[] hospitalPlayerAgent;                     // 플레이어 경로계산 AI 에이전트
    private Animator[] hospitalPlayersAnimator;                     // 플레이어 애니메이터 컴포넌트
    [SerializeField] private ParticleSystem[] hospitalPlayerRifles; // 플레이어 총 파티클
    private bool isHospitalPlayerInit;                              // 병원 플레이어 초기화 여부

    [SerializeField] private GameObject[] rescuePlayers;            // Man2(생존자), Girl1(생존자)
    [SerializeField] private Transform[] playersRescuePos;          // 플레이어 탈출 위치[Man2StartPos, Girl2StartPos, Man2TargetPos, Girl2TargetPos]
    [SerializeField] private GameObject[] rescuePlayerParentBloods; // 탈출 플레이어 피 파티클 부모 객체
    private GameObject[] rescuePlayerBloods;          // 교실 플레이어 피 파티클(Man1, Girl1)
    private NavMeshAgent[] rescuePlayerAgent;                       // 플레이어 경로계산 AI 에이전트
    private Animator[] rescuePlayersAnimator;                       // 플레이어 애니메이터 컴포넌트
    [SerializeField] private ParticleSystem[] rescuePlayerRifles;   // 플레이어 총 파티클
    private bool isRescuePlayerInit;                                // 탈출 플레이어 초기화 여부
    private bool isEscape;                                          // 탈출 여부

    [SerializeField] private GameObject rescueHelicopter;           // 구조 헬리콥터
    [SerializeField] private GameObject rescueHelicopterObj;        // 구조 헬리콥터 본체(하늘에 있는데 타겟에 도착하면 땅에 착륙)
    private NavMeshAgent rescueHelicopterAgent;                     // 구조 헬리콥터 AI 에이전트
    [SerializeField] private Transform rescueHelicopterPos;         // 구조 헬리콥터 도착 포인트
    private bool isHellcopterArrive;                                // 구조 헬리콥터 도착 여부

    private float playTime;                                         // 일반 좀비 죽을 시간
    private ZombieState[] normalZombieState;                        // 일반 좀비 상태
    private NavMeshAgent[] normalZombieAgent;                       // 플레이어 경로계산 AI 에이전트
    private Animator[] normalZombieAnimator;                        // 플레이어 애니메이터 컴포넌트
    [SerializeField] private GameObject[] normalZombies;            // 일반 좀비
    [SerializeField] private Transform[] normalZombiesTargetPos;    // 일반 좀비 위치
    [SerializeField] private GameObject[] normalZombieParentBloods; // 일반 좀비 피 파티클 부모 객체
    private GameObject[] normalZombieBloods;          // 일반 좀비 피 파티클
    private bool isNormalZombieInit;                                // 일반 좀비 초기화 여부

    private ZombieState bossZombieState;                            // 보스 좀비 상태
    private NavMeshAgent bossZombieAgent;                           // 보스 좀비 경로계산 AI 에이전트
    private Animator bossZombieAnimator;                            // 보스 좀비 애니메이터 컴포넌트
    [SerializeField] private GameObject bossZombie;                 // 보스 좀비
    [SerializeField] private Transform bossZombieTargetPos;         // 보스 좀비 위치
    [SerializeField] private GameObject[] bossZombieParentBloods;   // 보스 좀비 피 파티클 부모 객체
    private GameObject[] bossZombieBloods;            // 보스 좀비 피 파티클 
    private bool isBossZombieDie;                                   // 보스 좀비 죽음 여부
    private bool isBossZombieInit;                                  // 보스 좀비 초기화 여부

    private ZombieState[] waveZombieState;                          // 좀비웨이브 때 좀비들 상태
    private NavMeshAgent[] waveZombieAgent;                         // 좀비웨이브 때 좀비들 경로계산 AI 에이전트
    private Animator[] waveZombieAnimator;                          // 좀비웨이브 때 좀비들 애니메이터 컴포넌트
    [SerializeField] private GameObject[] waveZombies;              // 좀비웨이브 때 생성될 좀비들
    [SerializeField] private Transform[] waveZombiesTargetPos;      // 좀비웨이브 때 좀비들 위치
    [SerializeField] private GameObject[] waveZombieParentBloods;   // 좀비웨이브 때 좀비들 피 파티클
    private GameObject[] waveZombieBloods;            // 좀비웨이브 때 좀비들  피 파티클 
    private bool[] isWaveZombeDie;                                  // 좀비 죽음 여부 확인
    private bool isWaveZombieInit;                                  // 좀비웨이브 때 좀비 초기화 여부

    [SerializeField] GameObject endingPanel;                        // 엔딩 UI

    private bool isEnd = false;

    #endregion Variable

    #region Unity Method

    /// <summary>
    /// 컴포넌트 초기화
    /// </summary>
    private void Awake()
    {
        vaxAutoPlayState = VaxAutoPlayState.Init;

        // 초기화 완료 여부 false로 초기화
        isPlayerInit = false;
        isClassPlayerInit = false;
        isHospitalPlayerInit = false;
        isRescuePlayerInit = false;
        isNormalZombieInit = false;
        isBossZombieInit = false;
        isWaveZombieInit = false;
    }

    private void Start()
    {
        isEnd = false;
    }

    private void Update()
    {
        if (isEnd == false)
        {
            // 게임 오토 플레이
            AutoPaly();

            // 플레이어 위치 이동 여부 체크
            //PlayerPosChange();
        }
        else
        {
            if (endingPanel.activeSelf == false)
            {
                endingPanel.SetActive(true);
            }
        }
    }

    #endregion Unity Method

    #region Method

    /// <summary>
    ///  전체 초기화
    /// </summary>
    private void AllInit()
    {
        // 하나라도 초기화가 안되면 될 때가지 재 실행
        while (true)
        {
            // 플레이어 위치, 상태 등 초기화 작업
            PlayerInit();

            // 일반 좀비 위치, 상태 등 초기화 작업
            NormalZombieInit();

            // 보스 좀비 위치, 상태 등 초기화 작업
            BossZombieInit();

            // 좀비 웨이브 때 좀비 위치, 상태 등 초기화 작업
            WaveZombieInit();

            if (!isPlayerInit || !isClassPlayerInit || !isHospitalPlayerInit || !isRescuePlayerInit
            || !isNormalZombieInit || !isBossZombieInit || !isWaveZombieInit)
            {
                continue;
            }
            else
            {
                vaxAutoPlayState = VaxAutoPlayState.START;
                break;
            }
        }
    }

    /// <summary>
    /// 플레이어 위치, 상태 등 초기화 작업
    /// </summary>
    private void PlayerInit()
    {
        Debug.Log("PlayerInit 들어옴");

        // VAX 자동 플레이 진행 상태를 시작으로 초기화
        vaxAutoPlayState = VaxAutoPlayState.START;

        // 교실 플레이어 피 파티클 초기화
        classPlayerBloods = new GameObject[classPlayerParentBloods.Length];

        // 메인카메라 위치 학교-교실로 초기화
        mainCamera.transform.position = mainCameraPos[0].position;

        // 플레이어 상태 초기화
        playersState = new PlayerState[classPlayers.Length];

        // 플레이어 에이전트 초기화
        classPlayerAgent = new NavMeshAgent[classPlayers.Length - 1];
        hospitalPlayerAgent = new NavMeshAgent[hospitalPlayers.Length];
        rescuePlayerAgent = new NavMeshAgent[rescuePlayers.Length];

        // 플레이어 애니메이터 초기화
        classPlayersAnimator = new Animator[classPlayers.Length];
        hospitalPlayersAnimator = new Animator[hospitalPlayers.Length];
        rescuePlayersAnimator = new Animator[rescuePlayers.Length];

        // 플레이어 상태값 초기화
        playersState[0] = PlayerState.ATTACK;        // Man1 좀비 공격
        playersState[1] = PlayerState.THINKING;      // Man2 칠판앞에서 생각중
        playersState[2] = PlayerState.ATTACK;        // Girl1 좀비 공격
        playersState[3] = PlayerState.ATTACK;        // Girl2-Maria 좀비 공격

        // 교실 플레이어 초기화
        for (int i = 0; i < classPlayers.Length; i++)
        {
            // 플레이어 위치 학교-교실로 초기화
            classPlayers[i].transform.position = playersClassPos[i].position;

            // 피 파티클 자식 컴포넌트 가져오기
            classPlayerBloods[i] = classPlayerParentBloods[i].transform.Find("Blood").gameObject;

            // 플레이어 애니메이터 초기화
            classPlayersAnimator[i] = classPlayers[i].GetComponent<Animator>();

            if (i == 1)
            {
                classPlayersAnimator[i].SetBool("IsThinking", true);
            }

            // Girl2 = 마피아는 네브메쉬 없음
            if (i != classPlayers.Length - 1)
            {
                // 플레이어 에이전트 초기화
                classPlayerAgent[i] = classPlayers[i].GetComponent<NavMeshAgent>();

                // 플레이어 에이전트 스피드 설정
                classPlayerAgent[i].speed = runSpeed;

                // 플레이어 움직임 정지
                classPlayerAgent[i].isStopped = true;
            }

            // 교실 플레이어 스폰 완료 되었을 때 실행
            if (i == classPlayers.Length - 1)
            {
                isClassPlayerInit = true;
            }
        }

        // 병원 플레이어 초기화
        for (int i = 0; i < hospitalPlayers.Length; i++)
        {
            // 플레이어 위치 병원으로 초기화
            //hospitalPlayers[i].transform.position = playersHospitalPos[i].position;

            // 플레이어 애니메이터 초기화
            hospitalPlayersAnimator[i] = hospitalPlayers[i].GetComponent<Animator>();

            // 플레이어 걷기
            hospitalPlayersAnimator[i].SetBool("IsMove", true);

            // 플레이어 에이전트 초기화
            hospitalPlayerAgent[i] = hospitalPlayers[i].GetComponent<NavMeshAgent>();
            hospitalPlayerAgent[i].SetDestination(playersHospitalPos[i].position);

            // 플레이어 에이전트 스피드 설정
            hospitalPlayerAgent[i].speed = runSpeed * 4;

            // 플레이어 움직임 정지
            hospitalPlayerAgent[i].isStopped = true;

            Debug.Log("초기화 Hospital Player Count : " + i);

            // 병원 플레이어 스폰 완료 되었을 때 실행
            if (i == hospitalPlayers.Length - 1)
            {
                isHospitalPlayerInit = true;
            }
        }

        // 병원 문 초기화
        hospitalDoorAnim.SetBool("Door", true);



        // 탈출 플레이어 피 파티클 초기화
        rescuePlayerBloods = new GameObject[rescuePlayerParentBloods.Length];

        // 피 파티클 자식 컴포넌트 가져오기
        for (int i = 0; i < rescuePlayerParentBloods.Length; i++)
        {
            rescuePlayerBloods[i] = rescuePlayerParentBloods[i].transform.Find("Blood").gameObject;
        }

        // 학교 운동장 탈출 플레이어 초기화
        for (int i = 0; i < rescuePlayers.Length; i++)
        {
            // 플레이어 애니메이터 초기화
            rescuePlayersAnimator[i] = rescuePlayers[i].GetComponent<Animator>();

            // 플레이어 에이전트 초기화
            rescuePlayerAgent[i] = rescuePlayers[i].GetComponent<NavMeshAgent>();
            rescuePlayerAgent[i].SetDestination(playersRescuePos[i].position);

            // 플레이어 움직임 정지
            rescuePlayerAgent[i].isStopped = true;

            // 플레이어 에이전트 스피드 설정
            rescuePlayerAgent[i].speed = runSpeed * 7.7f;

            // 탈출 플레이어 스폰 완료 되었을 때 실행
            if (i == rescuePlayers.Length - 1)
            {
                isRescuePlayerInit = true;
            }
        }

        rescueHelicopterAgent = rescueHelicopter.GetComponent<NavMeshAgent>();
        isHellcopterArrive = false;

        isEscape = false;
        isPlayerInit = true;
    }

    /// <summary>
    /// 일반 좀비 위치, 상태 등 초기화 작업
    /// </summary>
    private void NormalZombieInit()
    {
        // 일반 좀비 상태 초기화
        normalZombieState = new ZombieState[normalZombies.Length];

        // 플레이어 에이전트 초기화
        normalZombieAgent = new NavMeshAgent[normalZombies.Length];

        // 플레이어 애니메이터 초기화
        normalZombieAnimator = new Animator[normalZombies.Length];

        // 일반 좀비 피 파티클 초기화
        normalZombieBloods = new GameObject[normalZombieParentBloods.Length];

        // 일반 좀비 초기화
        for (int i = 0; i < normalZombies.Length; i++)
        {
            // 일반 좀비 상태값 초기화
            normalZombieState[i] = ZombieState.IDLE;

            // 피 파티클 자식 컴포넌트 가져오기
            normalZombieBloods[i] = normalZombieParentBloods[i].transform.Find("Blood").gameObject;

            // 일반 좀비 에이전트 초기화
            normalZombieAgent[i] = normalZombies[i].GetComponent<NavMeshAgent>();

            // 일반 좀비 에이전트 스피드 설정
            normalZombieAgent[i].speed = runSpeed;

            // 움직이지 못하게 설정
            normalZombieAgent[i].isStopped = true;

            // 일반 좀비 타겟 위치 초기화
            normalZombieAgent[i].SetDestination(normalZombiesTargetPos[i].position);

            // 일반 좀비 애니메이터 초기화
            normalZombieAnimator[i] = normalZombies[i].GetComponent<Animator>();

            normalZombieAnimator[i].SetInteger("RandomWalk", Random.Range(0, 8));


            // 일반 좀비 스폰 완료 되었을 때 실행
            if (i == normalZombies.Length - 1)
            {
                isNormalZombieInit = true;
            }
        }

        normalZombieAgent[0].isStopped = false;  // normalZombieAgent[0] 죽으면 이동 예정
        normalZombieAgent[1].isStopped = false;  // normalZombieAgent[1] 죽으면 이동 예정
    }

    /// <summary>
    /// 보스 좀비 위치, 상태 등 초기화 작업
    /// </summary>
    private void BossZombieInit()
    {
        // 보스 좀비 죽음 여부
        isBossZombieDie = false;

        // 보스 좀비 상태 초기화
        bossZombieState = ZombieState.IDLE;

        // 보스 좀비 피 파티클 초기화
        bossZombieBloods = new GameObject[bossZombieParentBloods.Length];
        bossZombieBloods[0] = bossZombieParentBloods[0].transform.Find("Blood").gameObject;
        bossZombieBloods[1] = bossZombieParentBloods[1].transform.Find("Blood").gameObject;

        // 보스 좀비 에이전트 초기화
        bossZombieAgent = bossZombie.GetComponent<NavMeshAgent>();

        // 마피아 플레이어가 죽으면 이동 예정
        bossZombieAgent.isStopped = true;

        // 보스 좀비 타겟위치 초기화
        bossZombieAgent.SetDestination(bossZombieTargetPos.position);

        // 플레이어 에이전트 스피드 설정
        bossZombieAgent.speed = runSpeed * 4;

        // 보스 좀비 애니메이터 초기화
        bossZombieAnimator = bossZombie.GetComponent<Animator>();

        // 보스 좀비 스폰 완료 되었을 때 실행
        isBossZombieInit = true;
    }

    /// <summary>
    /// 좀비 웨이브 때 좀비위치, 상태 등 초기화 작업
    /// </summary>
    private void WaveZombieInit()
    {

        // 좀비웨이브 좀비 상태 초기화
        waveZombieState = new ZombieState[waveZombies.Length];

        // 좀비웨이브 피 파티클 초기화
        waveZombieBloods = new GameObject[waveZombieParentBloods.Length];

        // 좀비 죽음 여부 초기화
        isWaveZombeDie = new bool[waveZombies.Length];

        // 좀비웨이브 좀비  에이전트 초기화
        waveZombieAgent = new NavMeshAgent[waveZombies.Length];

        // 좀비웨이브 좀비  애니메이터 초기화
        waveZombieAnimator = new Animator[waveZombies.Length];

        // 좀비웨이브 좀비 초기화
        for (int i = 0; i < waveZombies.Length; i++)
        {
            // 좀비웨이브 상태값 초기화
            waveZombieState[i] = ZombieState.IDLE;

            // 피 파티클 자식 컴포넌트 가져오기
            waveZombieBloods[i] = waveZombieParentBloods[i].transform.Find("Blood").gameObject;

            // 좀비웨이브 에이전트 초기화
            waveZombieAgent[i] = waveZombies[i].GetComponent<NavMeshAgent>();

            // 좀비웨이브 에이전트 스피드 설정
            waveZombieAgent[i].speed = runSpeed;

            // 좀비웨이브 에이전트 이동 못하게 설정
            waveZombieAgent[i].isStopped = true;

            // 좀비웨이브 위치 초기화
            waveZombieAgent[i].SetDestination(waveZombiesTargetPos[i].position);

            // 좀비웨이브 애니메이터 초기화
            waveZombieAnimator[i] = waveZombies[i].GetComponent<Animator>();

            waveZombieAnimator[i].SetInteger("RandomWalk", Random.Range(0, 8));

            // 좀비웨이브 스폰 완료 되었을 때 실행
            if (i == waveZombies.Length - 1)
            {
                isWaveZombieInit = true;
            }
        }
    }

    /// <summary>
    /// 게임 진행도에 따른 네브메쉬 타겟 설정
    /// </summary>
    private void AutoPaly()
    {
        if (vaxAutoPlayState == VaxAutoPlayState.Init)
        {
            AllInit();
        }
        // 게임 시작 : 일반좀비 죽이기
        if (vaxAutoPlayState == VaxAutoPlayState.START)
        {
            // 게임 진행 시간
            playTime += Time.deltaTime;

            // 일반 좀비 수 만큼 반복
            for (int i = 0; i < normalZombies.Length; i++)
            {
                // agent.remainingDistance : 현재 agent 위치와 원하는 위치의 사이거리 값
                // agent.stoppingDistance : 도착지점 거리
                // 지정한 위치에 도착하면 공격 애니메이션
                if (normalZombieAgent[i].remainingDistance <= normalZombieAgent[i].stoppingDistance
                    && normalZombieAnimator[i].GetBool("IsDie") == false)
                {
                    normalZombieAnimator[i].SetBool("IsAttack", true);
                }
            }

            // 플레이타임 조건
            if (playTime > 4 && playTime < 5)
            {
                // 교실 플레이어 수 만큼 반복
                for (int i = 0; i < classPlayers.Length; i++)
                {
                    // Man2(생각중) 플레이어 제외하고 공격 애니메이션 활성화
                    if (classPlayersAnimator[i].GetBool("IsAttack") == false && i != 1
                        && normalZombieAnimator[2].GetBool("IsDie") == false)
                    {
                        classPlayersAnimator[i].SetBool("IsAttack", true);
                    }
                }

                // 슈팅 파티클 활성화
                if (classPlayerRifles[0].isPlaying == false)
                {
                    classPlayerRifles[0].Play();
                    classPlayerRifles[2].Play();

                    normalZombieBloods[0].SetActive(true);
                    normalZombieBloods[1].SetActive(true);
                }
            }
            else if (playTime >= 7 && playTime < 13)
            {
                // 일반 좀비1 애니메이션 죽음 처리
                if (normalZombieAnimator[0].GetBool("IsDie") == false)
                {
                    normalZombieAnimator[0].SetBool("IsDie", true);
                    normalZombieBloods[0].GetComponent<BloodParticleReactivator>().Stop();
                }

                // 좀비3 출발
                normalZombieAgent[2].isStopped = false;
                normalZombieBloods[2].SetActive(true);
            }
            else if (playTime >= 16 && playTime < 16.5)
            {
                // 좀비2 애니메이션 죽음 처리
                if (normalZombieAnimator[1].GetBool("IsDie") == false)
                {
                    normalZombieAnimator[1].SetBool("IsDie", true); 
                    normalZombieBloods[1].GetComponent<BloodParticleReactivator>().Stop();
                }

                // 좀비4 출발
                normalZombieAgent[3].isStopped = false;
                normalZombieBloods[3].SetActive(true);
            }
            else if (playTime >= 17 && playTime < 22)
            {
                // 좀비3 애니메이션 죽음 처리
                if (normalZombieAnimator[2].GetBool("IsDie") == false)
                {
                    normalZombieAnimator[2].SetBool("IsDie", true);
                    normalZombieBloods[2].GetComponent<BloodParticleReactivator>().Stop();

                    // 좀비3 쳐다보도록 설정
                    classPlayers[0].transform.LookAt(normalZombieAnimator[3].transform);
                    classPlayers[3].transform.LookAt(normalZombieAnimator[3].transform);
                }
            }
            else if (playTime >= 25)
            {
                // 좀비4 애니메이션 죽음 처리
                if (normalZombieAnimator[3].GetBool("IsDie") == false)
                {
                    // Man1(생존자), Girl1(생존자), Girl2(마피아) 공격 애니메이션 비활성화
                    classPlayersAnimator[0].SetBool("IsAttack", false);
                    classPlayersAnimator[2].SetBool("IsAttack", false);
                    classPlayersAnimator[3].SetBool("IsAttack", false);

                    // 교실 플레이어 슈팅 파티클 해제
                    classPlayerRifles[0].Stop();
                    classPlayerRifles[2].Stop();

                    normalZombieAnimator[3].SetBool("IsDie", true);
                    normalZombieBloods[3].GetComponent<BloodParticleReactivator>().Stop();

                    vaxAutoPlayState = VaxAutoPlayState.NORMAL_ZOMBIES_DEAD;
                    playTime = 0;
                }
            }
        }
        // 일반 좀비 죽은 후 : Girl2(마파아)가 Man1(생존자) 죽임
        else if (vaxAutoPlayState == VaxAutoPlayState.NORMAL_ZOMBIES_DEAD)
        {
            // 게임 진행 시간
            playTime += Time.deltaTime;

            // Man1 생존자가 죽지 않았을 때 실행
            if (classPlayersAnimator[0].GetBool("IsDie") == false)
            {
                // Grirl2(마피아)가 Man1 생존자 공격 시작
                if (playTime > 2 && playTime < 3.5)
                {
                    // Girl2(마피아)가 공격 대상 Man1을 보도록 설정
                    classPlayers[3].transform.LookAt(classPlayers[0].transform);

                    // Girl2(마피아)가 공격 애니메이션이 꺼져있으면 켜기
                    if (classPlayersAnimator[3].GetBool("IsAttack") == false)
                    {
                        classPlayersAnimator[3].SetBool("IsAttack", true);

                        // Girl2(마피아) 슈팅 파티클 재생
                        if (classPlayerRifles[2].isPlaying == false) classPlayerRifles[2].Play();

                        // Man1(생존자) 피 파티클 활성화
                        classPlayerBloods[0].SetActive(true);
                    }
                }
                // 공격당한 Man1이 Girl2(마피아) 공격
                else if (playTime >= 3.5 && playTime < 6)
                {
                    // Man1(생존자)가 Girl2(마피아)를 보도록 설정
                    classPlayers[0].transform.LookAt(classPlayers[3].transform);

                    // Man1(생존자)가 공격 애니메이션이 꺼져있으면 켜기
                    if (classPlayersAnimator[0].GetBool("IsAttack") == false)
                    {
                        classPlayersAnimator[0].SetBool("IsAttack", true);

                        // Man1(생존자) 슈팅 파티클 재생
                        if (classPlayerRifles[0].isPlaying == false) classPlayerRifles[0].Play();
                    }
                }

                // Girl2(마피아)가 Man1 생존자를 공격하는 모습을 다른 생존자가 보고 놀람
                if (playTime >= 5.5f && playTime < 8)
                {
                    // Man2(생존자)가 Girl2(마피아)를 보도록 설정
                    classPlayers[1].transform.LookAt(classPlayers[3].transform);
                    // Girl1(생존자)가 Girl2(마피아)를 보도록 설정
                    classPlayers[2].transform.LookAt(classPlayers[3].transform);

                    // Man1(생존자) 생각중 애니메이션 해제, 놀람 애니메이션 재생
                    classPlayersAnimator[1].SetBool("IsThinking", false);
                    classPlayersAnimator[1].SetBool("IsSurprised", true);

                    // Girl1(생존자) 놀람 애니메이션 재생
                    classPlayersAnimator[2].SetBool("IsSurprised", true);
                }

                if (playTime >= 10)
                {
                    // Man1 죽음으로 변경
                    classPlayersAnimator[0].SetBool("IsDie", true);

                    // Main1(생존자) 피 파티클 종료
                    classPlayerBloods[0].SetActive(false);
                    classPlayerBloods[0].GetComponent<BloodParticleReactivator>().Stop();

                    classPlayerRifles[0].Stop();

                    // Girl2(마피아)가 공격 대상 Girl1 보도록 설정
                    classPlayers[3].transform.LookAt(classPlayers[2].transform);

                    // Girl2(마피아) 잠시 공격 멈춤
                    classPlayersAnimator[3].SetBool("IsAttack", false);

                    // Girl2(마피아) 슈팅 파티클 비활성화
                    classPlayerRifles[2].Stop();
                }
            }
            // Man1(생존자)가 죽었을 때 실행
            else
            {
                if (playTime >= 11 && playTime < 16)
                {
                    // Man2(생존자) 놀람 애니메이션 비활성화, 공격 애니메이션 활성화
                    classPlayersAnimator[1].SetBool("IsSurprised", false);
                    classPlayersAnimator[1].SetBool("IsAttack", true);

                    // Man2(생존자) 슈팅 파티클 활성화
                    if (classPlayerRifles[1].isPlaying == false)
                    {
                        classPlayerRifles[1].Play();

                        // Girl2(마피아) 피 파티클 활성화
                        classPlayerBloods[3].SetActive(true);
                    }

                    // Girl1(생존자) 놀람 애니메이션 비활성화, 공격 애니메이션 활성화
                    classPlayersAnimator[2].SetBool("IsSurprised", false);
                    classPlayersAnimator[2].SetBool("IsAttack", true);

                    // Girl2(마피아) 공격 애니메이션 활성화
                    classPlayersAnimator[3].SetBool("IsAttack", true);

                    // Girl2(마피아) 슈팅 파티클 활성화
                    if (classPlayerRifles[2].isPlaying == false)
                    {
                        classPlayerRifles[2].Play();

                        // Girl1(생존자) 피 파티클 활성화
                        classPlayerBloods[2].SetActive(true);
                    }
                }
                else if (playTime >= 17)
                {
                    // 네브메쉬 타겟 Girl2(마피아) 지정
                    classPlayerAgent[2].SetDestination(classPlayers[3].transform.position);
                    // 네브메쉬 추격 시작
                    classPlayerAgent[2].isStopped = false;

                    // 게임진행 상태 변경
                    vaxAutoPlayState = VaxAutoPlayState.MAN1_DEAD;

                    // 플레이 타임 초기화
                    playTime = 0;
                }
            }
        }
        // Man1(생존자) 죽은 후 남은 생존자들이 Girl2(마피아) 죽임
        else if (vaxAutoPlayState == VaxAutoPlayState.MAN1_DEAD)
        {
            // 게임 진행 시간
            playTime += Time.deltaTime;

            // Girl2(마피아)가 죽지 않았을 때 실행
            if (classPlayersAnimator[3].GetBool("IsDie") == false)
            {
                if (playTime > 4)
                {
                    // Girl2(마피아) 죽음 처리
                    classPlayersAnimator[3].SetBool("IsDie", true);
                    // 슈팅 파티클 비활성화
                    classPlayerRifles[2].Stop();

                    // Man2(생존자), Girl1(생존자) 보스 좀비 바라보도록 설정
                    classPlayers[1].transform.LookAt(bossZombie.transform);
                    classPlayers[2].transform.LookAt(bossZombie.transform);

                    // Man2(생존자), Girl1(생존자) 공격 애니메이션 비활성화
                    classPlayersAnimator[1].SetBool("IsAttack", false);
                    classPlayersAnimator[2].SetBool("IsAttack", false);

                    // 슈팅 파티클 비활성화
                    classPlayerRifles[1].Stop();

                    // Girl1(생존자), Girl2(마피아) 피 파티클 종료
                    classPlayerBloods[2].GetComponent<BloodParticleReactivator>().Stop();
                    classPlayerBloods[2].SetActive(false);
                    classPlayerBloods[3].GetComponent<BloodParticleReactivator>().Stop();
                    classPlayerBloods[3].SetActive(false);
                }
            }
            // Girl2(마피아)가 죽었을 때 실행
            else
            {
                if (playTime >= 6.5f && playTime < 8f)
                {
                    // Man2(생존자), Girl1(생존자) 놀람 애니메이션 실행
                    classPlayersAnimator[1].SetBool("IsSurprised", true);
                    classPlayersAnimator[2].SetBool("IsSurprised", true);
                }
                else if (playTime >= 8)
                {
                    // Man2(생존자), Girl1(생존자) 놀람 애니메이션 비활성화
                    classPlayersAnimator[1].SetBool("IsSurprised", false);
                    classPlayersAnimator[2].SetBool("IsSurprised", false);

                    // 게임 진행 상태 마피아 죽은 후로 변경
                    vaxAutoPlayState = VaxAutoPlayState.MAFIA_DEAD;

                    // 보스 좀비 출발
                    bossZombieAgent.isStopped = false;

                    // 플레이타임 초기화
                    playTime = 0;
                }
            }
        }
        // Girl2(마피아) 죽은 후 등장한 보스 좀비 죽이기
        else if (vaxAutoPlayState == VaxAutoPlayState.MAFIA_DEAD)
        {
            //Debug.Log("MAN1_DEAD 보스 좀비 등장");

            // 게임 진행 시간
            playTime += Time.deltaTime;

            // agent.remainingDistance : 현재 agent 위치와 원하는 위치의 사이거리 값
            // agent.stoppingDistance : 도착지점 거리
            // 지정한 위치에 도착하면 공격 애니메이션
            if (bossZombieAgent.remainingDistance <= bossZombieAgent.stoppingDistance
                && isBossZombieDie == false)
            {
                // 보스 좀비 공격 애니메이션 활성화
                bossZombieAnimator.SetBool("IsAttack", true);

                // Man2(생존자), Girl1(생존자) 보스 좀비에게 공격 시작
                classPlayersAnimator[1].SetBool("IsAttack", true);
                classPlayersAnimator[2].SetBool("IsAttack", true);

                // Man2(생존자) 슈팅 파티클 활성화
                if (classPlayerRifles[1].isPlaying == false) classPlayerRifles[1].Play();

                // Man2(생존자), Girl1(생존자), 보스 좀비 피 파티클 활성화
                classPlayerBloods[1].SetActive(true);
                bossZombieBloods[0].SetActive(true);
                bossZombieBloods[1].SetActive(true);

                // Girl1(생존자) 보스 좀비를 네브메쉬로 타겟으로 설정
                classPlayerAgent[2].SetDestination(bossZombie.transform.position);

                // Girl1(생존자) 추격 시작
                if (classPlayerAgent[2].remainingDistance > 0.2f)
                {
                    classPlayerAgent[2].isStopped = true;
                }
            }

            // 보스 좀비가 죽지 않았을 때 실행
            if (isBossZombieDie == false)
            {
                // Man2(생존자), Girl1(생존자) 보스 좀비 바라보도록 설정
                classPlayers[1].transform.LookAt(bossZombie.transform);
                classPlayers[2].transform.LookAt(bossZombie.transform);

                // Girl1(생존자) 보스 좀비를 네브메쉬로 타겟으로 설정
                classPlayerAgent[2].SetDestination(bossZombie.transform.position);

                // Girl1(생존자) 보스 좀비와의 거리가 0.5f 미만으로 좁혀지면 멈춤
                if (classPlayerAgent[2].remainingDistance < 0.5f)
                {
                    classPlayerAgent[2].isStopped = false;
                }

                // 플레이타임 18초 이상 되면 보스 좀비 죽음
                if (playTime > 18)
                {
                    isBossZombieDie = true;

                    // Man2(생존자), Girl1(생존자), 보스 좀비 피 파티클 비활성화
                    classPlayerBloods[1].GetComponent<BloodParticleReactivator>().Stop();
                    classPlayerBloods[1].SetActive(false);
                    bossZombieBloods[0].GetComponent<BloodParticleReactivator>().Stop();
                    bossZombieBloods[0].SetActive(false);
                    bossZombieBloods[1].GetComponent<BloodParticleReactivator>().Stop();
                    bossZombieBloods[1].SetActive(false);
                }
            }
            // 보스 좀비가 죽었을 때 실행
            else if (isBossZombieDie == true)
            {
                // 병원키 비활성화 상태일 때 실행
                if (hospitalKey.activeSelf == false && playTime < 22)
                {
                    // 보스 좀비 죽음 애니메이션 활성화
                    bossZombieAnimator.SetBool("IsDie", true);

                    // Man2(생존자), Girl1(생존자) 공격 애니메이션 비활성화
                    classPlayersAnimator[1].SetBool("IsAttack", false);
                    classPlayersAnimator[2].SetBool("IsAttack", false);

                    // Man2(생존자) 슈팅 파티클 비활성화
                    classPlayerRifles[1].Stop();

                    // 카메라 보고 웃도록 설정
                    classPlayers[1].transform.LookAt(mainCamera.transform.position);
                    classPlayers[2].transform.LookAt(mainCamera.transform.position);

                    // Man2(생존자), Girl1(생존자) 기쁨 애니메이션 활성화
                    classPlayersAnimator[1].SetBool("IsJoy", true);
                    classPlayersAnimator[2].SetBool("IsJoy", true);

                    // 병원 키 활성화
                    hospitalKey.SetActive(true);
                }
                // 병원키 활성화 되었을 때 실행
                else
                {
                    if (playTime > 25)
                    {
                        // 병원 키 비활성화
                        hospitalKey.SetActive(false);

                        // 학교 교실 플레이어 오브젝트 삭제
                        for (int i = 0; i < classPlayers.Length; i++)
                        {
                            Destroy(classPlayers[i].gameObject, 2f);
                        }

                        // 학교 일반 좀비 오브젝트 삭제
                        for (int i = 0; i < normalZombies.Length; i++)
                        {
                            Destroy(normalZombies[i].gameObject, 2f);
                        }

                        // 보스 좀비 삭제
                        Destroy(bossZombie.gameObject, 2f);

                        // 메인카메라 병원으로 이동
                        mainCamera.transform.position = mainCameraPos[1].position;
                        mainCamera.transform.rotation = mainCameraPos[1].rotation;

                        // 히든 병원 키 병원 위치로 이동(문여는 용도로 사용함)
                        hospitalKey.transform.position = hospitalKeyPos.position;

                        // 병원 문 열기
                        hospitalDoorAnim.SetBool("Door", false);

                        // 병원 플레이어 타겟 설정 및 이동 설정
                        //hospitalPlayerAgent[0].SetDestination(playersHospitalPos[0].position);
                        //hospitalPlayerAgent[1].SetDestination(playersHospitalPos[1].position);
                        hospitalPlayerAgent[0].isStopped = false;
                        hospitalPlayerAgent[1].isStopped = false;

                        // 게임 진행 상태 변경
                        vaxAutoPlayState = VaxAutoPlayState.BOSS_ZOMBIE_DEAD;

                        // 게임 진행 넘기기 위해 플레이 타임 강제 조정
                        playTime = 0;
                    }
                }
            }
        }
        // 보스 좀비 죽었으면 히든 열쇠로 병원문 열고 들어가서 백신 찾는다.
        else if (vaxAutoPlayState == VaxAutoPlayState.BOSS_ZOMBIE_DEAD)
        {
            // 게임 진행 시간
            playTime += Time.deltaTime;

            // Girl1(생존자)가 타겟 포인트에 도착 했을 때 실행
            if (hospitalPlayerAgent[1].remainingDistance <= hospitalPlayerAgent[1].stoppingDistance
                && vax.activeSelf == false && hospitalPlayersAnimator[0].GetBool("IsLookFor") == false)
            {
                // 플레이어 멈추고 걷기 애니메이션 비활성화
                hospitalPlayerAgent[0].isStopped = true;
                hospitalPlayerAgent[1].isStopped = true;
                hospitalPlayersAnimator[0].SetBool("IsMove", false);
                hospitalPlayersAnimator[1].SetBool("IsMove", false);

                // 찾기 애니메이션 활성화
                hospitalPlayersAnimator[0].SetBool("IsLookFor", true);
                hospitalPlayersAnimator[1].SetBool("IsLookFor", true);
            }
            else
            {
                if (playTime >= 10 && playTime < 17)
                {
                    vax.SetActive(true);

                    // 카메라 보고 웃도록 설정
                    hospitalPlayers[0].transform.LookAt(mainCamera.transform.position);
                    hospitalPlayers[1].transform.LookAt(mainCamera.transform.position);

                    // 찾기 애니메이션 비활성화되고 자동으로 기쁨 애니메이션 진행
                    hospitalPlayersAnimator[0].SetBool("IsLookFor", false);
                    hospitalPlayersAnimator[1].SetBool("IsLookFor", false);
                    hospitalPlayersAnimator[0].SetBool("IsJoy", true);
                    hospitalPlayersAnimator[1].SetBool("IsJoy", true);

                    // 탈출 플레이어 기쁨 애니메이션 활성화
                    /*rescuePlayersAnimator[0].SetBool("IsJoy", true);
                    rescuePlayersAnimator[1].SetBool("IsJoy", true);*/
                }
                else if (playTime > 17)
                {
                    // 병원 관련 오브젝트 삭제
                    Destroy(hospitalPlayers[0].gameObject, 2f);
                    Destroy(hospitalPlayers[1].gameObject, 2f);
                    Destroy(hospitalKey.gameObject, 2f);
                    Destroy(vax.gameObject, 2f);

                    // 카메라 위치 학교-운동장 이동
                    mainCamera.transform.position = mainCameraPos[2].position;
                    mainCamera.transform.rotation = mainCameraPos[2].rotation;

                    // 신호탄 오브젝트 활성화
                    firework.SetActive(true);

                    // 좀비 웨이브 시작
                    for (int i = 0; i < waveZombies.Length; i++)
                    {
                        waveZombieAgent[i].isStopped = false;
                    }

                    // 게임 진행상태 변경
                    vaxAutoPlayState = VaxAutoPlayState.RESCUE_REQEUST;

                    // 플레이 타임 초기화
                    playTime = 0;
                }
            }

        }
        // 학교-운동장에서 신호탄 터트린 후 헬기가 올 때까지 살아님기
        else if (vaxAutoPlayState == VaxAutoPlayState.RESCUE_REQEUST)
        {
            // 게임 진행 시간
            playTime += Time.deltaTime;

            // 탈출 성공했을 때
            if(playTime > 5 && isEscape == true)
            {
                Debug.Log("탈출 성공 게임 트레일러 종료");
                isEnd= true;
            }

            // 좀비 웨이브의 좀비 수 만큼 반복
            for (int i = 0; i < waveZombies.Length; i++)
            {
                // 해당 좀비가 살아있고 타겟 포인트에 도착하면 실행
                if (waveZombieAgent[i].remainingDistance <= waveZombieAgent[i].stoppingDistance
                && waveZombieAnimator[i].GetBool("IsDie") == false)
                {
                    // 공격 애니메이션이 아닐 때 활성화로 변경
                    if (waveZombieAnimator[i].GetBool("IsAttack") == false)
                    {
                        waveZombieAnimator[i].SetBool("IsAttack", true);
                    }
                    waveZombieAgent[i].isStopped = true;
                }

                // 좀비 죽음 여부 체크
                if (isWaveZombeDie[i] == true)
                {
                    // 죽음 애니메이션이 아닐 때 활성화
                    if (waveZombieAnimator[i].GetBool("IsDie") == false)
                    {
                        waveZombieAnimator[i].SetBool("IsDie", true);
                        waveZombieBloods[i].SetActive(false);
                        waveZombieBloods[i].GetComponent<BloodParticleReactivator>().Stop();
                    }
                }

                // Man2(생존자)가 죽으면 전체 좀비 
                if(rescuePlayersAnimator[0].GetBool("IsDie") == true)
                {
                    // 좀비 죽음 여부 체크
                    if (isWaveZombeDie[i] == false)
                    {
                        waveZombieBloods[i].SetActive(false);
                        waveZombieBloods[i].GetComponent<BloodParticleReactivator>().Stop();
                    }
                }
            }

            // Man2(생존자)가 죽지 않았을 때 실행
            if (rescuePlayersAnimator[0].GetBool("IsDie") == false)
            {
                if (playTime > 3 && playTime < 5)
                {
                    // 카메라 보고 웃도록 설정
                    rescuePlayers[0].transform.LookAt(mainCamera.transform.position);
                    rescuePlayers[1].transform.LookAt(mainCamera.transform.position);

                    rescuePlayersAnimator[0].SetBool("IsJoy", true);
                    rescuePlayersAnimator[1].SetBool("IsJoy", true);
                }
                else if (playTime > 5 && playTime < 6)
                {
                    rescueHelicopter.SetActive(true);

                    // 구조 헬리콥터 춟라
                    rescueHelicopterAgent.SetDestination(rescueHelicopterPos.position);
                    rescueHelicopterAgent.isStopped = false;

                    // 플레이어 운동장 입구 좀비 막으러 출발
                    rescuePlayerAgent[0].isStopped = false;
                    rescuePlayerAgent[1].isStopped = false;

                    // 웃음 애니메이션 비활성화, 이동 애니메이션 활성화
                    rescuePlayersAnimator[0].SetBool("IsJoy", false);
                    rescuePlayersAnimator[1].SetBool("IsJoy", false);
                    rescuePlayersAnimator[0].SetBool("IsMove", true);
                    rescuePlayersAnimator[1].SetBool("IsMove", true);

                    // 좀비 스피드 빠르게 변경
                    for (int i = 0; i < waveZombies.Length; i++)
                    {
                        waveZombieAgent[i].speed = runSpeed * 2;
                    }
                }
                // Man2(생존자)가 운동장 입구 좀비 막으로 도착 하면 공격 애니메이션 활성화
                if (rescuePlayerAgent[0].remainingDistance <= rescuePlayerAgent[0].stoppingDistance)
                {
                    // 공격 애니메이션이 활성화 안되었을 때만 실행
                    if (rescuePlayersAnimator[0].GetBool("IsAttack") == false)
                    {
                        // Man2(생존자) 멈추고 공격 시작
                        rescuePlayerAgent[0].isStopped = true;
                        rescuePlayersAnimator[0].SetBool("IsMove", false);
                        rescuePlayersAnimator[0].SetBool("IsAttack", true);
                        rescuePlayerRifles[0].Play();
                        waveZombieBloods[1].SetActive(true);
                        waveZombieBloods[3].SetActive(true);
                        Debug.Log("Man1 공격 시작");

                        rescuePlayerBloods[0].SetActive(true);
                    }
                }

                // Girl1(생존자)가 운동장 입구 좀비 막으로 도착 하면 공격 애니메이션 활성화
                if (rescuePlayerAgent[1].remainingDistance <= rescuePlayerAgent[1].stoppingDistance)
                {
                    // 공격 애니메이션이 활성화 안되었을 때만 실행
                    if (rescuePlayersAnimator[1].GetBool("IsAttack") == false)
                    {
                        // Girl1(생존자) 멈추고 공격 시작
                        //rescuePlayerAgent[1].isStopped = true;
                        rescuePlayersAnimator[1].SetBool("IsMove", false);
                        rescuePlayersAnimator[1].SetBool("IsAttack", true);
                        rescuePlayerBloods[1].SetActive(true);
                        waveZombieBloods[0].SetActive(true);
                        waveZombieBloods[2].SetActive(true);
                        Debug.Log("Girl1 공격 시작");
                    }
                    rescuePlayers[1].transform.LookAt(waveZombiesTargetPos[1]);
                }

                // 좀비 1 죽음
                if (playTime >= 15 && playTime < 16)
                {
                    isWaveZombeDie[0] = true;
                    Debug.Log("좀비 1 죽음");
                    waveZombieBloods[4].SetActive(true);
                }
                // 좀비 2 죽음
                else if (playTime >= 18 && playTime < 19)
                {
                    isWaveZombeDie[1] = true;
                    Debug.Log("좀비 2 죽음");
                    waveZombieBloods[5].SetActive(true);
                }
                // 좀비 3 죽음
                else if (playTime >= 21 && playTime < 22)
                {
                    isWaveZombeDie[2] = true;
                    Debug.Log("좀비3 :죽음");
                    waveZombieBloods[6].SetActive(true);
                }
                // 좀비 3 죽음
                else if (playTime >= 25 && playTime < 26)
                {
                    isWaveZombeDie[3] = true;
                    Debug.Log("좀비4 죽음");
                    waveZombieBloods[7].SetActive(true);
                }
                // 좀비 5 죽음
                else if (playTime >= 28 && playTime < 29)
                {
                    isWaveZombeDie[4] = true;
                    Debug.Log("좀비5 죽음");
                }
                // Man1 죽음
                else if (playTime >= 30)
                {
                    rescuePlayersAnimator[0].SetBool("IsDie", true);
                    rescuePlayerRifles[0].Stop();
                    rescuePlayerBloods[0].GetComponent<BloodParticleReactivator>().Stop();
                }
            }
            else
            {
                // 살아있는 좀비 타겟 Girl1로 설정
                for (int i = 0; i < isWaveZombeDie.Length; i++)
                {
                    if (isWaveZombeDie[i] == false && playTime > 34)
                    {
                        waveZombieAgent[i].SetDestination(playersRescuePos[2].position);
                        waveZombieAgent[i].isStopped = false;
                    }
                }

                // 구조 헬리 콥터가 도착했을 때 실행
                if (rescueHelicopterAgent.remainingDistance <= rescueHelicopterAgent.stoppingDistance
                    && isHellcopterArrive == false)
                {
                    // Girl1(생존자) 이동 목적지 탈출 포인트로 지정
                    rescuePlayerAgent[1].SetDestination(playersRescuePos[2].position);
                    rescuePlayers[1].transform.LookAt(playersRescuePos[2]);
                    // 이동 시작
                    rescuePlayerAgent[1].isStopped = false;
                    // 이동 애니메이션 활성화
                    if (rescuePlayersAnimator[1].GetBool("IsMove") == false)
                    {
                        // Girl1(생존자) 피 파티클 비활성화
                        rescuePlayerBloods[1].GetComponent<BloodParticleReactivator>().Stop();

                        rescuePlayersAnimator[1].SetBool("IsAttack", false);
                        rescuePlayersAnimator[1].SetBool("IsMove", true);
                    }

                    // 헬리 콥터 착륙 시작
                    if(rescueHelicopterObj.transform.position.y > 0)
                    {
                        rescueHelicopterObj.transform.position = new Vector3(rescueHelicopterObj.transform.position.x, rescueHelicopterObj.transform.position.y -0.1f, rescueHelicopterObj.transform.position.z);
                    }
                    // 헬리 콥터 착륙 완료
                    else
                    {
                        Debug.Log("헬리콥터 도착");
                        isHellcopterArrive = true;
                    }    
                }

                // Girl1(생존자)가 탈출 포인트에 도착 했을 때 실행
                if (isHellcopterArrive == true && isEscape == false
                    && rescuePlayerAgent[1].remainingDistance <= rescuePlayerAgent[1].stoppingDistance)
                {
                    Debug.Log("Girl1 헬기 도착");

                    rescuePlayerAgent[1].isStopped = true;
                    rescuePlayersAnimator[1].SetBool("IsMove", false);
                    rescuePlayersAnimator[1].SetBool("IsJoy", true);

                    // 카메라 보고 웃도록 설정
                    rescuePlayers[1].transform.LookAt(mainCamera.transform.position);
                    isEscape = true;
                    playTime = 0;
                }
            }
        }
    }

    #endregion Method
}