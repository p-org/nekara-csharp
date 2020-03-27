#include "pch.h"
#include "NekaraService.h"

// NS::NekaraService* _ns;
NS::NekaraService* _nsj;

// C# Bindings
extern "C" {
    __declspec(dllexport) HANDLE NS_NekaraService() {
        typedef NS::NekaraService* HANDLE;
        HANDLE ns = new NS::NekaraService();
        return ns;
    }

    __declspec(dllexport) void NS_Attach(HANDLE ns_hande) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->Attach();
    }

    __declspec(dllexport) void NS_Detach(HANDLE ns_hande) {
        // assert(_ns != NULL && "Nekara Testing Service not Initialized");
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->Detach();
    }

    __declspec(dllexport) bool NS_IsDetached(HANDLE ns_hande) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        return ns->IsDetached();
    }

    __declspec(dllexport) void NS_CreateTask(HANDLE ns_hande) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->CreateThread();
    }

    __declspec(dllexport) void NS_StartTask(HANDLE ns_hande, int _threadID) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->StartThread(_threadID);
    }

    __declspec(dllexport) void NS_EndTask(HANDLE ns_hande, int _threadID) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->EndThread(_threadID);
    }

    __declspec(dllexport) void NS_CreateResource(HANDLE ns_hande, int _resourceID) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->CreateResource(_resourceID);
    }

    __declspec(dllexport) void NS_DeleteResource(HANDLE ns_hande, int _resourceID) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->DeleteResource(_resourceID);
    }

    __declspec(dllexport) void NS_BlockedOnResource(HANDLE ns_hande, int _resourceID) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->BlockedOnResource(_resourceID);
    }

    __declspec(dllexport) void NS_BlockedOnAnyResource(HANDLE ns_hande, int _resourceID[], int _size) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->BlockedOnAnyResource(_resourceID, _size);
    }

    __declspec(dllexport) void NS_SignalUpdatedResource(HANDLE ns_hande, int _resourceID) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->SignalUpdatedResource(_resourceID);
    }

    __declspec(dllexport) bool NS_CreateNondetBool(HANDLE ns_hande) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        return ns->CreateNondetBool();
    }

    __declspec(dllexport) int NS_CreateNondetInteger(HANDLE ns_hande, int _maxvalue) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        return ns->CreateNondetInteger(_maxvalue);
    }

    __declspec(dllexport) void NS_ContextSwitch(HANDLE ns_hande) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->ContextSwitch();
    }

    __declspec(dllexport) void NS_WaitforMainTask(HANDLE ns_hande) {
        NS::NekaraService* ns = (NS::NekaraService*)ns_hande;
        ns->WaitforMainTask();
    }

    /* __declspec(dllexport) bool NS_Dispose() {
        _ns = NULL;
        return true;
    } */
}



// JAVA Bindings
/* #include "C:\Users\t-arut\eclipse-workspace\TestProject\src\NekaraServiceJava.h"
#include "C:\Program Files\Java\jdk1.8.0_241\include\jni.h"
#include "C:\Program Files\Java\jdk1.8.0_241\include\win32\jni_md.h"


extern "C" {
    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1WithoutSeed(JNIEnv* env, jobject obj) {
        _nsj = new NS::NekaraService();
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1WithSeed(JNIEnv* env, jobject obj, jint _seed) {
        _nsj = new NS::NekaraService(_seed);
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1CreateThread(JNIEnv* env, jobject obj) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->CreateThread();
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1StartThread(JNIEnv* env, jobject obj, jint _threadID) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->StartThread(_threadID);
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1EndThread(JNIEnv* env, jobject obj, jint _threadID) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->EndThread(_threadID);
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1ContextSwitch(JNIEnv* env, jobject obj) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->ContextSwitch();
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1WaitforMainTask(JNIEnv* env, jobject obj) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->WaitforMainTask();
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1CreateResource(JNIEnv* env, jobject obj, jint _resourceID) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->CreateResource(_resourceID);
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1DeleteResource(JNIEnv* env, jobject obj, jint _resourceID) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->DeleteResource(_resourceID);
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1BlockedOnResource(JNIEnv* env, jobject obj, jint _resourceID) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->BlockedOnResource(_resourceID);
    }

    // TODO: Bug! convert jint Array to int* [].
    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1BlockedOnAnyResource(JNIEnv* env, jobject obj, jintArray _resourceID, jint _size) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        // _nsj->BlockedOnAnyResource(_resourceID, _size);
    }

    JNIEXPORT void JNICALL Java_NekaraServiceJava_NSJ_1SignalUpdatedResource(JNIEnv* env, jobject obj, jint _resourceID) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        _nsj->SignalUpdatedResource(_resourceID);
    }

    JNIEXPORT jboolean  JNICALL Java_NekaraServiceJava_NSJ_1CreateNondetBool(JNIEnv* env, jobject obj) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        return _nsj->CreateNondetBool();
    }

    JNIEXPORT jint JNICALL Java_NekaraServiceJava_NSJ_1CreateNondetInteger(JNIEnv* env, jobject obj, jint _maxValue) {
        assert(_nsj != NULL && "Nekara Testing Service not Initialized");
        return _nsj->CreateNondetInteger(_maxValue);
    }

    JNIEXPORT jboolean JNICALL Java_NekaraServiceJava_NSJ_1Dispose(JNIEnv* env, jobject obj) {
        _nsj = NULL;
        return true;
    } 

}*/