mergeInto(LibraryManager.library, {
    TriggerWebGLFilePicker: function (objectNamePtr, methodNamePtr, fileTypePtr) {
        var objectName = Pointer_stringify(objectNamePtr);
        var methodName = Pointer_stringify(methodNamePtr);
        var fileType = Pointer_stringify(fileTypePtr);

        // Hapus input lama jika ada sisa
        var oldInput = document.getElementById('unity-file-picker');
        if (oldInput) document.body.removeChild(oldInput);

        // Buat elemen input file HTML5
        var fileInput = document.createElement('input');
        fileInput.id = 'unity-file-picker';
        fileInput.type = 'file';
        fileInput.accept = fileType; // Contoh: 'video/mp4' atau 'audio/mpeg'
        fileInput.style.display = 'none';

        fileInput.onchange = function (event) {
            var file = event.target.files[0];
            if (!file) return;

            // Buat URL lokal sementara (Blob URL) agar bisa dibaca UnityWebRequest
            var blobUrl = URL.createObjectURL(file);

            // Kirim URL blob kembali ke object dan fungsi di Unity
            SendMessage(objectName, methodName, blobUrl);
            
            // Bersihkan elemen setelah selesai
            document.body.removeChild(fileInput);
        };

        document.body.appendChild(fileInput);
        fileInput.click();
    }
});