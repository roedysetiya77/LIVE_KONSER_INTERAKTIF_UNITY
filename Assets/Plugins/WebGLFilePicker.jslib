mergeInto(LibraryManager.library, {
    TriggerWebGLFilePicker: function (objectNamePtr, methodNamePtr, fileTypePtr) {
        // Menggunakan UTF8ToString untuk Unity versi modern agar string terbaca dengan benar
        var objectName = UTF8ToString(objectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);
        var fileType = UTF8ToString(fileTypePtr);

        console.log("📂 [JS BROWSER] Mencoba memicu File Picker untuk tipe: " + fileType);

        // Hapus input lama jika tersisa di halaman web
        var oldInput = document.getElementById('unity-file-picker');
        if (oldInput) {
            oldInput.parentNode.removeChild(oldInput);
        }

        // Buat elemen input file HTML5 secara dinamis
        var fileInput = document.createElement('input');
        fileInput.id = 'unity-file-picker';
        fileInput.type = 'file';
        fileInput.accept = fileType;
        fileInput.style.display = 'none';

        fileInput.onchange = function (event) {
            var file = event.target.files[0];
            if (!file) {
                console.warn("⚠️ [JS BROWSER] Pemilihan file dibatalkan oleh user.");
                return;
            }

            console.log("🎵 [JS BROWSER] File berhasil dipilih: " + file.name + " (" + file.size + " bytes)");

            // Buat URL lokal sementara (Blob URL)
            var blobUrl = URL.createObjectURL(file);
            console.log("🔗 [JS BROWSER] Blob URL dibuat: " + blobUrl);

            // Kirim balik URL Blob ke objek Unity yang memanggilnya
            SendMessage(objectName, methodName, blobUrl);
            
            // Bersihkan elemen input dari DOM browser
            if (fileInput.parentNode) {
                fileInput.parentNode.removeChild(fileInput);
            }
        };

        document.body.appendChild(fileInput);
        
        // Pemicu klik fisik browser
        fileInput.click();
    }
});