(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const carousels = document.querySelectorAll('.matter-info-carousel');
        if (!carousels.length) return;

        carousels.forEach(initCarousel);
    });

    function initCarousel(carouselContainer) {
        let currentSlide = 0;
        let isScrolling = false;
        let scrollTimeout;

        const slider = carouselContainer.querySelector('.carousel-slider');
        if (!slider) return;
        const slides = slider.querySelectorAll('.carousel-slide');
        const totalSlides = slides.length;

        const prevBtn = carouselContainer.querySelector('.carousel-prev');
        const nextBtn = carouselContainer.querySelector('.carousel-next');
        const dots = carouselContainer.querySelectorAll('.carousel-dot');

        updateCarousel();

        if (prevBtn) {
            prevBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                navigateToSlide(currentSlide - 1);
            });
        }

        if (nextBtn) {
            nextBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                navigateToSlide(currentSlide + 1);
            });
        }

        dots.forEach((dot, index) => {
            dot.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                navigateToSlide(index);
            });
        });

        // Wheel navigation
        slider.addEventListener('wheel', function (e) {
            if (isScrolling) return;
            if (Math.abs(e.deltaY) > 0 || Math.abs(e.deltaX) > 0) {
                e.preventDefault();
                e.stopPropagation();

                isScrolling = true;
                clearTimeout(scrollTimeout);
                const delta = e.deltaY || e.deltaX;

                if (delta > 30) navigateToSlide(currentSlide + 1);
                else if (delta < -30) navigateToSlide(currentSlide - 1);

                scrollTimeout = setTimeout(function () { isScrolling = false; }, 400);
            }
        }, { passive: false });

        // Touch/swipe
        let touchStartX = 0;
        let touchEndX = 0;
        slider.addEventListener('touchstart', function (e) {
            touchStartX = e.changedTouches[0].screenX;
        }, { passive: true });
        slider.addEventListener('touchend', function (e) {
            touchEndX = e.changedTouches[0].screenX;
            const diff = touchStartX - touchEndX;
            const threshold = 50;
            if (Math.abs(diff) > threshold) {
                if (diff > 0) navigateToSlide(currentSlide + 1);
                else navigateToSlide(currentSlide - 1);
            }
        }, { passive: true });

        function navigateToSlide(index) {
            if (index < 0 || index >= totalSlides) return;
            currentSlide = index;
            updateCarousel();
        }

        function updateCarousel() {
            const offset = -currentSlide * 100;
            slider.style.transform = 'translateX(' + offset + '%)';

            dots.forEach((dot, index) => {
                if (index === currentSlide) dot.classList.add('active');
                else dot.classList.remove('active');
            });

            if (prevBtn) {
                prevBtn.disabled = currentSlide === 0;
                prevBtn.style.opacity = currentSlide === 0 ? '0.3' : '1';
                prevBtn.style.cursor = currentSlide === 0 ? 'not-allowed' : 'pointer';
            }
            if (nextBtn) {
                nextBtn.disabled = currentSlide === totalSlides - 1;
                nextBtn.style.opacity = currentSlide === totalSlides - 1 ? '0.3' : '1';
                nextBtn.style.cursor = currentSlide === totalSlides - 1 ? 'not-allowed' : 'pointer';
            }
        }
    }
})();

