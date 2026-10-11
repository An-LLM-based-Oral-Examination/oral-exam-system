import { useEffect, useRef } from 'react'
import * as THREE from 'three'

interface FloatingAccent {
  mesh: THREE.Mesh
  baseY: number
  phase: number
  speed: number
}

function createSeededRandom(seed = 4_903) {
  let currentSeed = seed

  return () => {
    currentSeed = (currentSeed * 16_807) % 2_147_483_647
    return (currentSeed - 1) / 2_147_483_646
  }
}

function createRoundedPanelGeometry(width: number, height: number, radius: number, depth: number) {
  const halfWidth = width / 2
  const halfHeight = height / 2
  const shape = new THREE.Shape()

  shape.moveTo(-halfWidth + radius, -halfHeight)
  shape.lineTo(halfWidth - radius, -halfHeight)
  shape.quadraticCurveTo(halfWidth, -halfHeight, halfWidth, -halfHeight + radius)
  shape.lineTo(halfWidth, halfHeight - radius)
  shape.quadraticCurveTo(halfWidth, halfHeight, halfWidth - radius, halfHeight)
  shape.lineTo(-halfWidth + radius, halfHeight)
  shape.quadraticCurveTo(-halfWidth, halfHeight, -halfWidth, halfHeight - radius)
  shape.lineTo(-halfWidth, -halfHeight + radius)
  shape.quadraticCurveTo(-halfWidth, -halfHeight, -halfWidth + radius, -halfHeight)

  const geometry = new THREE.ExtrudeGeometry(shape, {
    depth,
    bevelEnabled: true,
    bevelSegments: 3,
    bevelSize: 0.025,
    bevelThickness: 0.025,
    curveSegments: 16,
    steps: 1,
  })
  geometry.translate(0, 0, -depth / 2)
  return geometry
}

function createOrbit(radiusX: number, radiusY: number, color: number, opacity: number) {
  const points: THREE.Vector3[] = []

  for (let index = 0; index < 128; index += 1) {
    const angle = (index / 128) * Math.PI * 2
    points.push(new THREE.Vector3(Math.cos(angle) * radiusX, Math.sin(angle) * radiusY, 0))
  }

  return new THREE.LineLoop(
    new THREE.BufferGeometry().setFromPoints(points),
    new THREE.LineBasicMaterial({
      color,
      opacity,
      transparent: true,
      depthWrite: false,
    }),
  )
}

export function LecturerOverviewThreeScene() {
  const mountRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const mount = mountRef.current
    if (!mount) return

    const random = createSeededRandom()
    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(35, 1, 0.1, 100)
    camera.position.set(0, 0.05, 9.6)

    let renderer: THREE.WebGLRenderer

    try {
      renderer = new THREE.WebGLRenderer({
        alpha: true,
        antialias: true,
        powerPreference: 'high-performance',
      })
    } catch {
      return
    }

    renderer.setClearColor(0x000000, 0)
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.05
    renderer.domElement.setAttribute('aria-hidden', 'true')
    renderer.domElement.style.display = 'block'
    renderer.domElement.style.width = '100%'
    renderer.domElement.style.height = '100%'
    mount.appendChild(renderer.domElement)

    const hemisphereLight = new THREE.HemisphereLight(0xf1fdff, 0x071a3c, 1.6)
    const keyLight = new THREE.DirectionalLight(0xe3fbff, 3.4)
    const cobaltLight = new THREE.PointLight(0x3468ff, 12, 11, 1.7)
    const cyanLight = new THREE.PointLight(0x55e5ff, 10, 10, 1.75)
    keyLight.position.set(4, 5, 7)
    cobaltLight.position.set(-3.2, 0.5, 3.2)
    cyanLight.position.set(3.4, 2.1, 2.8)
    scene.add(hemisphereLight, keyLight, cobaltLight, cyanLight)

    const root = new THREE.Group()
    root.position.set(0.12, 0.05, 0)
    root.scale.setScalar(0.92)
    scene.add(root)

    const halo = new THREE.Group()
    halo.position.z = -1.35
    root.add(halo)

    const haloDisc = new THREE.Mesh(
      new THREE.CircleGeometry(2.75, 72),
      new THREE.MeshBasicMaterial({
        color: 0x55dff8,
        opacity: 0.045,
        transparent: true,
        depthWrite: false,
      }),
    )
    const haloRing = new THREE.Mesh(
      new THREE.RingGeometry(1.96, 2.01, 80),
      new THREE.MeshBasicMaterial({
        color: 0x66e8ff,
        opacity: 0.16,
        transparent: true,
        side: THREE.DoubleSide,
        depthWrite: false,
      }),
    )
    halo.add(haloDisc, haloRing)

    const orbitSystem = new THREE.Group()
    orbitSystem.position.z = -0.8
    root.add(orbitSystem)

    const orbitOneGroup = new THREE.Group()
    orbitOneGroup.rotation.set(0.42, 0.15, -0.26)
    orbitOneGroup.add(createOrbit(3.15, 1.68, 0x63ddf5, 0.28))
    const orbitTwoGroup = new THREE.Group()
    orbitTwoGroup.rotation.set(-0.62, 0.18, 0.76)
    orbitTwoGroup.add(createOrbit(2.9, 1.4, 0x6d86ff, 0.24))
    orbitSystem.add(orbitOneGroup, orbitTwoGroup)

    const panelMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x0b3f92,
      emissive: 0x071d54,
      emissiveIntensity: 0.24,
      metalness: 0.5,
      roughness: 0.24,
      clearcoat: 0.9,
      clearcoatRoughness: 0.15,
    })
    const panelFaceMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x0c234b,
      emissive: 0x061b3d,
      emissiveIntensity: 0.42,
      metalness: 0.35,
      roughness: 0.3,
      clearcoat: 0.72,
      transparent: true,
      opacity: 0.96,
    })
    const cyanMaterial = new THREE.MeshBasicMaterial({
      color: 0x6be9f8,
      transparent: true,
      opacity: 0.9,
    })
    const cobaltMaterial = new THREE.MeshBasicMaterial({
      color: 0x6686ff,
      transparent: true,
      opacity: 0.88,
    })
    const mutedMaterial = new THREE.MeshBasicMaterial({
      color: 0x6c8ebc,
      transparent: true,
      opacity: 0.52,
    })

    const studio = new THREE.Group()
    studio.rotation.set(-0.04, -0.04, -0.015)
    root.add(studio)

    const leftPanel = new THREE.Mesh(
      createRoundedPanelGeometry(2.5, 3.05, 0.24, 0.2),
      panelMaterial,
    )
    leftPanel.position.set(-1.17, 0.05, 0)
    leftPanel.rotation.y = 0.16
    const leftFace = new THREE.Mesh(
      createRoundedPanelGeometry(2.28, 2.8, 0.18, 0.055),
      panelFaceMaterial,
    )
    leftFace.position.z = 0.14
    leftPanel.add(leftFace)

    const rightPanel = new THREE.Mesh(
      createRoundedPanelGeometry(2.5, 3.05, 0.24, 0.2),
      panelMaterial,
    )
    rightPanel.position.set(1.17, 0.05, 0)
    rightPanel.rotation.y = -0.16
    const rightFace = new THREE.Mesh(
      createRoundedPanelGeometry(2.28, 2.8, 0.18, 0.055),
      panelFaceMaterial,
    )
    rightFace.position.z = 0.14
    rightPanel.add(rightFace)
    studio.add(leftPanel, rightPanel)

    const hinge = new THREE.Mesh(
      new THREE.CapsuleGeometry(0.085, 2.58, 8, 16),
      new THREE.MeshStandardMaterial({
        color: 0xb4edf6,
        emissive: 0x2b7186,
        emissiveIntensity: 0.28,
        metalness: 0.84,
        roughness: 0.2,
      }),
    )
    hinge.position.set(0, 0.05, 0.28)
    studio.add(hinge)

    const rubricContent = new THREE.Group()
    rubricContent.position.set(-1.14, 0.62, 0.31)
    rubricContent.rotation.y = 0.16
    studio.add(rubricContent)

    const rubricTitle = new THREE.Mesh(
      createRoundedPanelGeometry(1.28, 0.14, 0.06, 0.025),
      cyanMaterial,
    )
    rubricTitle.position.set(-0.16, 0.46, 0)
    rubricContent.add(rubricTitle)

    const rubricBars: THREE.Mesh[] = []
    const rubricWidths = [1.35, 0.98, 1.48, 1.16]
    rubricWidths.forEach((width, index) => {
      const track = new THREE.Mesh(
        createRoundedPanelGeometry(1.62, 0.12, 0.055, 0.02),
        mutedMaterial,
      )
      track.position.set(0, 0.08 - index * 0.38, 0)
      rubricContent.add(track)

      const bar = new THREE.Mesh(
        createRoundedPanelGeometry(width, 0.12, 0.055, 0.03),
        index % 2 === 0 ? cyanMaterial : cobaltMaterial,
      )
      bar.position.set(-(1.62 - width) / 2, 0.08 - index * 0.38, 0.035)
      rubricContent.add(bar)
      rubricBars.push(bar)
    })

    const scoreNodes = new THREE.Group()
    scoreNodes.position.set(-1.14, -1.08, 0.36)
    scoreNodes.rotation.y = 0.16
    studio.add(scoreNodes)
    for (let index = 0; index < 10; index += 1) {
      const node = new THREE.Mesh(
        new THREE.BoxGeometry(0.125, 0.125, 0.07),
        index < 8 ? cyanMaterial : cobaltMaterial,
      )
      node.position.x = (index - 4.5) * 0.185
      node.rotation.z = index % 2 === 0 ? 0.08 : -0.08
      scoreNodes.add(node)
    }

    const evidenceContent = new THREE.Group()
    evidenceContent.position.set(1.14, 0.18, 0.31)
    evidenceContent.rotation.y = -0.16
    studio.add(evidenceContent)

    const evidenceRing = new THREE.Mesh(new THREE.TorusGeometry(0.62, 0.045, 12, 64), cyanMaterial)
    evidenceRing.position.y = 0.48
    evidenceContent.add(evidenceRing)

    const playCore = new THREE.Mesh(
      new THREE.CircleGeometry(0.2, 36),
      new THREE.MeshBasicMaterial({
        color: 0x5f7cff,
        transparent: true,
        opacity: 0.82,
        side: THREE.DoubleSide,
      }),
    )
    playCore.position.set(0, 0.48, 0.025)
    evidenceContent.add(playCore)

    const waveformBars: THREE.Mesh[] = []
    const waveformHeights = [0.24, 0.46, 0.72, 0.38, 0.86, 0.56, 0.3, 0.68, 0.42]
    waveformHeights.forEach((height, index) => {
      const bar = new THREE.Mesh(
        createRoundedPanelGeometry(0.1, height, 0.045, 0.035),
        index % 3 === 0 ? cobaltMaterial : cyanMaterial,
      )
      bar.position.set((index - 4) * 0.19, -0.57, 0.02)
      evidenceContent.add(bar)
      waveformBars.push(bar)
    })

    const floatingAccents: FloatingAccent[] = []
    const accentGeometry = new THREE.OctahedronGeometry(0.075, 1)
    for (let index = 0; index < 16; index += 1) {
      const angle = random() * Math.PI * 2
      const radius = 2.65 + random() * 0.72
      const accent = new THREE.Mesh(accentGeometry, index % 2 === 0 ? cyanMaterial : cobaltMaterial)
      accent.position.set(
        Math.cos(angle) * radius,
        Math.sin(angle) * radius * 0.58,
        -0.35 + random() * 1.15,
      )
      accent.scale.setScalar(0.55 + random() * 0.75)
      root.add(accent)
      floatingAccents.push({
        mesh: accent,
        baseY: accent.position.y,
        phase: random() * Math.PI * 2,
        speed: 0.6 + random() * 0.55,
      })
    }

    const particleCount = 110
    const particlePositions = new Float32Array(particleCount * 3)
    for (let index = 0; index < particleCount; index += 1) {
      particlePositions[index * 3] = (random() - 0.5) * 7.4
      particlePositions[index * 3 + 1] = (random() - 0.5) * 4.6
      particlePositions[index * 3 + 2] = -1.8 + random() * 1.4
    }
    const particleGeometry = new THREE.BufferGeometry()
    particleGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3))
    const particles = new THREE.Points(
      particleGeometry,
      new THREE.PointsMaterial({
        color: 0xb5f5ff,
        opacity: 0.36,
        size: 0.026,
        transparent: true,
        depthWrite: false,
        sizeAttenuation: true,
      }),
    )
    root.add(particles)

    const targetPointer = new THREE.Vector2()
    const smoothPointer = new THREE.Vector2()
    const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)')
    let reduceMotion = motionPreference.matches
    let isDocumentVisible = document.visibilityState !== 'hidden'
    let isInViewport = true
    let scrollParallax = 0

    const renderFrame = (time = 0) => {
      const elapsed = reduceMotion ? 0 : time * 0.001
      smoothPointer.lerp(targetPointer, reduceMotion ? 1 : 0.045)

      root.rotation.y = smoothPointer.x * 0.09
      root.rotation.x = smoothPointer.y * -0.055
      root.position.x = 0.12 + smoothPointer.x * 0.1
      root.position.y = 0.05 + smoothPointer.y * 0.06 + scrollParallax * 0.18

      if (!reduceMotion) {
        studio.position.y = Math.sin(elapsed * 0.72) * 0.045
        studio.rotation.z = -0.015 + Math.sin(elapsed * 0.38) * 0.012
        orbitOneGroup.rotation.z = -0.26 + elapsed * 0.075
        orbitTwoGroup.rotation.z = 0.76 - elapsed * 0.058
        haloRing.rotation.z = elapsed * 0.035
        particles.rotation.z = elapsed * 0.018
        evidenceRing.rotation.z = elapsed * 0.16

        rubricBars.forEach((bar, index) => {
          bar.position.z = 0.035 + Math.sin(elapsed * 1.25 + index * 0.7) * 0.012
        })
        waveformBars.forEach((bar, index) => {
          const pulse = 0.94 + Math.sin(elapsed * 2.1 + index * 0.58) * 0.12
          bar.scale.y = pulse
        })
        floatingAccents.forEach(({ mesh, baseY, phase, speed }) => {
          mesh.position.y = baseY + Math.sin(elapsed * speed + phase) * 0.08
          mesh.rotation.x = elapsed * speed * 0.42 + phase
          mesh.rotation.y = elapsed * speed * 0.34
        })
      }

      renderer.render(scene, camera)
    }

    const syncAnimationLoop = () => {
      const shouldAnimate = !reduceMotion && isDocumentVisible && isInViewport
      renderer.setAnimationLoop(shouldAnimate ? renderFrame : null)

      if (!shouldAnimate && isDocumentVisible && isInViewport) {
        renderFrame(0)
      }
    }

    const handlePointerMove = (event: PointerEvent) => {
      if (reduceMotion) return

      const rect = mount.getBoundingClientRect()
      if (rect.width === 0 || rect.height === 0) return

      const relativeX = (event.clientX - rect.left) / rect.width
      const relativeY = (event.clientY - rect.top) / rect.height

      if (relativeX < -0.35 || relativeX > 1.35 || relativeY < -0.35 || relativeY > 1.35) {
        targetPointer.set(0, 0)
        return
      }

      targetPointer.set((relativeX - 0.5) * 2, (relativeY - 0.5) * 2)
    }

    const handleScroll = () => {
      if (reduceMotion) {
        scrollParallax = 0
        return
      }

      const rect = mount.getBoundingClientRect()
      const viewportCenter = window.innerHeight / 2
      const elementCenter = rect.top + rect.height / 2
      scrollParallax = THREE.MathUtils.clamp(
        (viewportCenter - elementCenter) / window.innerHeight,
        -1,
        1,
      )
    }

    const resizeScene = () => {
      const { width, height } = mount.getBoundingClientRect()
      if (width === 0 || height === 0) return

      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
      renderer.setSize(width, height, false)
      camera.aspect = width / height
      camera.updateProjectionMatrix()
      handleScroll()

      if (isDocumentVisible && isInViewport) renderFrame(0)
    }

    const handleVisibilityChange = () => {
      isDocumentVisible = document.visibilityState !== 'hidden'
      syncAnimationLoop()
    }

    const handleMotionPreference = (event: MediaQueryListEvent) => {
      reduceMotion = event.matches
      if (reduceMotion) {
        targetPointer.set(0, 0)
        smoothPointer.set(0, 0)
        scrollParallax = 0
      } else {
        handleScroll()
      }
      syncAnimationLoop()
    }

    const resizeObserver = new ResizeObserver(resizeScene)
    resizeObserver.observe(mount)

    const intersectionObserver = new IntersectionObserver(
      ([entry]) => {
        isInViewport = entry.isIntersecting
        syncAnimationLoop()
      },
      { threshold: 0.05 },
    )
    intersectionObserver.observe(mount)

    window.addEventListener('pointermove', handlePointerMove, { passive: true })
    window.addEventListener('scroll', handleScroll, { passive: true })
    document.addEventListener('visibilitychange', handleVisibilityChange)
    motionPreference.addEventListener('change', handleMotionPreference)

    resizeScene()
    syncAnimationLoop()

    return () => {
      renderer.setAnimationLoop(null)
      resizeObserver.disconnect()
      intersectionObserver.disconnect()
      window.removeEventListener('pointermove', handlePointerMove)
      window.removeEventListener('scroll', handleScroll)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
      motionPreference.removeEventListener('change', handleMotionPreference)

      const geometries = new Set<THREE.BufferGeometry>()
      const materials = new Set<THREE.Material>()
      scene.traverse((object) => {
        const renderable = object as THREE.Object3D & {
          geometry?: THREE.BufferGeometry
          material?: THREE.Material | THREE.Material[]
        }

        if (renderable.geometry) geometries.add(renderable.geometry)
        if (Array.isArray(renderable.material)) {
          renderable.material.forEach((material) => materials.add(material))
        } else if (renderable.material) {
          materials.add(renderable.material)
        }
      })

      geometries.forEach((geometry) => geometry.dispose())
      materials.forEach((material) => material.dispose())
      renderer.renderLists.dispose()
      renderer.dispose()
      renderer.forceContextLoss()

      if (renderer.domElement.parentElement === mount) {
        mount.removeChild(renderer.domElement)
      }
    }
  }, [])

  return <div ref={mountRef} className="pointer-events-none absolute inset-0" aria-hidden="true" />
}

export default LecturerOverviewThreeScene
