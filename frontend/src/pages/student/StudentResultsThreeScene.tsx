import { useEffect, useRef } from 'react'
import * as THREE from 'three'

interface StudentResultsThreeSceneProps {
  averageScore: number | null
  pendingCount: number
}

interface PulseNode {
  mesh: THREE.Mesh
  phase: number
  baseScale: number
}

function createSeededRandom(seed = 52_019) {
  let currentSeed = seed

  return () => {
    currentSeed = (currentSeed * 16_807) % 2_147_483_647
    return (currentSeed - 1) / 2_147_483_646
  }
}

function createRoundedPanelGeometry(width: number, height: number, radius: number, depth: number) {
  const shape = new THREE.Shape()
  const left = -width / 2
  const right = width / 2
  const bottom = -height / 2
  const top = height / 2

  shape.moveTo(left + radius, bottom)
  shape.lineTo(right - radius, bottom)
  shape.quadraticCurveTo(right, bottom, right, bottom + radius)
  shape.lineTo(right, top - radius)
  shape.quadraticCurveTo(right, top, right - radius, top)
  shape.lineTo(left + radius, top)
  shape.quadraticCurveTo(left, top, left, top - radius)
  shape.lineTo(left, bottom + radius)
  shape.quadraticCurveTo(left, bottom, left + radius, bottom)

  return new THREE.ExtrudeGeometry(shape, {
    depth,
    bevelEnabled: true,
    bevelSegments: 4,
    bevelSize: 0.025,
    bevelThickness: 0.025,
    curveSegments: 14,
    steps: 1,
  })
}

function createScoreTexture(averageScore: number | null, pendingCount: number) {
  const canvas = document.createElement('canvas')
  canvas.width = 512
  canvas.height = 360
  const context = canvas.getContext('2d')
  if (!context) return null

  const gradient = context.createLinearGradient(0, 0, 512, 360)
  gradient.addColorStop(0, '#082352')
  gradient.addColorStop(0.6, '#123f87')
  gradient.addColorStop(1, '#0b7390')
  context.fillStyle = gradient
  context.fillRect(0, 0, canvas.width, canvas.height)

  context.strokeStyle = 'rgba(116, 235, 245, 0.14)'
  context.lineWidth = 1
  for (let x = 0; x <= 512; x += 32) {
    context.beginPath()
    context.moveTo(x, 0)
    context.lineTo(x, 360)
    context.stroke()
  }
  for (let y = 0; y <= 360; y += 32) {
    context.beginPath()
    context.moveTo(0, y)
    context.lineTo(512, y)
    context.stroke()
  }

  context.fillStyle = '#85eff7'
  context.font = '700 23px system-ui, sans-serif'
  context.fillText('ACADEMIC SCORE', 34, 52)
  context.fillStyle = '#ffffff'
  context.font = '800 112px system-ui, sans-serif'
  context.fillText(averageScore === null ? '—' : averageScore.toFixed(1), 30, 183)
  context.fillStyle = '#b8d8f4'
  context.font = '600 31px system-ui, sans-serif'
  context.fillText('/ 10', 245, 180)

  context.fillStyle = '#43ddb0'
  context.beginPath()
  context.roundRect(34, 225, 175, 50, 25)
  context.fill()
  context.fillStyle = '#062f2b'
  context.font = '800 20px system-ui, sans-serif'
  context.fillText('LOCKED GRADES', 54, 257)

  context.fillStyle = 'rgba(255,255,255,0.1)'
  context.beginPath()
  context.roundRect(226, 225, 250, 50, 25)
  context.fill()
  context.fillStyle = '#d8e9f8'
  context.font = '700 19px system-ui, sans-serif'
  context.fillText(`${pendingCount} PENDING REVIEW`, 250, 257)

  const texture = new THREE.CanvasTexture(canvas)
  texture.colorSpace = THREE.SRGBColorSpace
  texture.anisotropy = 4
  return texture
}

function createOrbit(radiusX: number, radiusY: number, color: number, opacity: number) {
  const points: THREE.Vector3[] = []
  for (let index = 0; index < 128; index += 1) {
    const angle = (index / 128) * Math.PI * 2
    points.push(new THREE.Vector3(Math.cos(angle) * radiusX, Math.sin(angle) * radiusY, 0))
  }

  return new THREE.LineLoop(
    new THREE.BufferGeometry().setFromPoints(points),
    new THREE.LineBasicMaterial({ color, opacity, transparent: true, depthWrite: false }),
  )
}

export function StudentResultsThreeScene({
  averageScore,
  pendingCount,
}: StudentResultsThreeSceneProps) {
  const mountRef = useRef<HTMLDivElement>(null)
  const fallbackRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const mount = mountRef.current
    if (!mount) return
    const fallback = fallbackRef.current

    const random = createSeededRandom()
    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 60)
    camera.position.set(0, 0.15, 8.7)

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

    if (fallback) fallback.hidden = true

    renderer.setClearColor(0x000000, 0)
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.04
    renderer.domElement.setAttribute('aria-hidden', 'true')
    renderer.domElement.style.display = 'block'
    renderer.domElement.style.width = '100%'
    renderer.domElement.style.height = '100%'
    mount.appendChild(renderer.domElement)

    const hemisphereLight = new THREE.HemisphereLight(0xf0fdff, 0x07152f, 1.65)
    const keyLight = new THREE.DirectionalLight(0xe4fbff, 3.2)
    const cyanLight = new THREE.PointLight(0x5aeaf4, 12, 10, 1.7)
    const blueLight = new THREE.PointLight(0x345ee8, 9, 9, 1.8)
    const mintLight = new THREE.PointLight(0x45e1ad, 7, 7, 1.7)
    keyLight.position.set(4.5, 6, 5)
    cyanLight.position.set(2.7, 2.4, 3.2)
    blueLight.position.set(-3.1, -0.8, 2.6)
    mintLight.position.set(1.4, -1.1, 2.8)
    scene.add(hemisphereLight, keyLight, cyanLight, blueLight, mintLight)

    const rootGroup = new THREE.Group()
    rootGroup.position.set(0.42, 0, 0)
    rootGroup.scale.setScalar(0.92)
    scene.add(rootGroup)

    const navyMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x071b43,
      emissive: 0x061638,
      emissiveIntensity: 0.24,
      metalness: 0.7,
      roughness: 0.23,
      clearcoat: 0.88,
      clearcoatRoughness: 0.13,
    })
    const blueMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x245cd5,
      emissive: 0x123a93,
      emissiveIntensity: 0.42,
      metalness: 0.55,
      roughness: 0.2,
      clearcoat: 0.9,
      clearcoatRoughness: 0.12,
    })
    const cyanMaterial = new THREE.MeshStandardMaterial({
      color: 0x7af2f7,
      emissive: 0x2abfce,
      emissiveIntensity: 0.76,
      metalness: 0.3,
      roughness: 0.17,
    })
    const mintMaterial = new THREE.MeshStandardMaterial({
      color: 0x62e8b9,
      emissive: 0x23a77c,
      emissiveIntensity: 0.72,
      metalness: 0.28,
      roughness: 0.18,
    })
    const amberMaterial = new THREE.MeshStandardMaterial({
      color: 0xffd078,
      emissive: 0xd68a20,
      emissiveIntensity: 0.58,
      metalness: 0.36,
      roughness: 0.21,
    })

    const cardGeometry = createRoundedPanelGeometry(3.35, 2.32, 0.22, 0.13)
    cardGeometry.center()
    const cardBack = new THREE.Mesh(cardGeometry, navyMaterial)
    cardBack.rotation.set(-0.05, -0.18, -0.04)
    rootGroup.add(cardBack)

    const scoreTexture = createScoreTexture(averageScore, pendingCount)
    const scoreFace = new THREE.Mesh(
      new THREE.PlaneGeometry(3.07, 2.03),
      new THREE.MeshBasicMaterial({ map: scoreTexture, color: 0xffffff, toneMapped: false }),
    )
    scoreFace.position.set(-0.22, 0, 0.13)
    scoreFace.rotation.copy(cardBack.rotation)
    rootGroup.add(scoreFace)

    const cornerBadge = new THREE.Group()
    cornerBadge.position.set(1.68, -0.82, 0.42)
    rootGroup.add(cornerBadge)
    const badgeCore = new THREE.Mesh(new THREE.IcosahedronGeometry(0.43, 2), blueMaterial)
    cornerBadge.add(badgeCore)
    const badgeRing = new THREE.Mesh(new THREE.TorusGeometry(0.56, 0.035, 12, 60), cyanMaterial)
    badgeRing.rotation.x = 0.78
    cornerBadge.add(badgeRing)
    const badgeCheck = new THREE.Mesh(new THREE.TorusGeometry(0.19, 0.035, 10, 36), mintMaterial)
    badgeCheck.position.z = 0.34
    badgeCheck.scale.y = 0.68
    cornerBadge.add(badgeCheck)

    const chartGroup = new THREE.Group()
    chartGroup.position.set(-2.02, -0.82, -0.08)
    chartGroup.rotation.y = 0.12
    rootGroup.add(chartGroup)
    const chartBase = new THREE.Mesh(new THREE.BoxGeometry(1.45, 0.1, 0.72), navyMaterial)
    chartGroup.add(chartBase)
    const barHeights = [0.62, 1.03, 0.78, 1.3]
    const bars: THREE.Mesh[] = []
    barHeights.forEach((height, index) => {
      const bar = new THREE.Mesh(
        new THREE.BoxGeometry(0.22, height, 0.24),
        index === barHeights.length - 1 ? mintMaterial : index === 1 ? cyanMaterial : blueMaterial,
      )
      bar.position.set(-0.48 + index * 0.32, height / 2 + 0.05, 0)
      chartGroup.add(bar)
      bars.push(bar)
    })

    const orbitBack = new THREE.Group()
    orbitBack.position.z = -0.85
    rootGroup.add(orbitBack)
    const orbitOneGroup = new THREE.Group()
    orbitOneGroup.rotation.set(0.54, 0.2, -0.22)
    orbitOneGroup.add(createOrbit(2.72, 1.25, 0x60e8f2, 0.34))
    const orbitTwoGroup = new THREE.Group()
    orbitTwoGroup.rotation.set(-0.48, -0.25, 0.82)
    orbitTwoGroup.add(createOrbit(2.5, 1.08, 0x7587f2, 0.27))
    orbitBack.add(orbitOneGroup, orbitTwoGroup)

    const nodeMaterial = new THREE.MeshStandardMaterial({
      color: 0xc9fbff,
      emissive: 0x4dd7e5,
      emissiveIntensity: 0.86,
      metalness: 0.18,
      roughness: 0.18,
    })
    const pulseNodes: PulseNode[] = []
    for (let index = 0; index < 8; index += 1) {
      const angle = (index / 8) * Math.PI * 2 + 0.2
      const baseScale = 0.68 + random() * 0.5
      const node = new THREE.Mesh(new THREE.SphereGeometry(0.065, 14, 14), nodeMaterial)
      node.position.set(Math.cos(angle) * 2.65, Math.sin(angle) * 1.2, 0)
      node.scale.setScalar(baseScale)
      orbitBack.add(node)
      pulseNodes.push({ mesh: node, phase: index * 0.72, baseScale })
    }

    const pendingOrb = new THREE.Mesh(new THREE.OctahedronGeometry(0.28, 1), amberMaterial)
    pendingOrb.position.set(2.1, 1.02, 0.1)
    rootGroup.add(pendingOrb)

    const particleCount = 36
    const particlePositions = new Float32Array(particleCount * 3)
    for (let index = 0; index < particleCount; index += 1) {
      particlePositions[index * 3] = (random() - 0.5) * 6.4
      particlePositions[index * 3 + 1] = (random() - 0.5) * 3.5
      particlePositions[index * 3 + 2] = (random() - 0.5) * 2.5 - 0.5
    }
    const particleGeometry = new THREE.BufferGeometry()
    particleGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3))
    const particles = new THREE.Points(
      particleGeometry,
      new THREE.PointsMaterial({
        color: 0x69eaf3,
        size: 0.04,
        opacity: 0.46,
        transparent: true,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
      }),
    )
    rootGroup.add(particles)

    const pointerTarget = new THREE.Vector2()
    const pointerCurrent = new THREE.Vector2()
    const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)')
    let reduceMotion = motionPreference.matches
    let isDocumentVisible = document.visibilityState !== 'hidden'
    let isInViewport = true
    let mountRect = mount.getBoundingClientRect()
    const startedAt = performance.now()
    const cardRotation = cardBack.rotation.clone()
    const scoreRotation = scoreFace.rotation.clone()
    const orbitOneRotation = orbitOneGroup.rotation.clone()
    const orbitTwoRotation = orbitTwoGroup.rotation.clone()

    const resetPose = () => {
      pointerTarget.set(0, 0)
      pointerCurrent.set(0, 0)
      rootGroup.position.set(0.42, 0, 0)
      rootGroup.rotation.set(0, 0, 0)
      cardBack.rotation.copy(cardRotation)
      scoreFace.rotation.copy(scoreRotation)
    }

    const renderFrame = (time = performance.now()) => {
      const elapsed = (time - startedAt) * 0.001

      if (!reduceMotion && isDocumentVisible && isInViewport) {
        pointerCurrent.lerp(pointerTarget, 0.055)
        rootGroup.position.x = 0.42 + pointerCurrent.x * 0.16
        rootGroup.position.y = -pointerCurrent.y * 0.09
        rootGroup.rotation.y = pointerCurrent.x * 0.12
        rootGroup.rotation.x = -pointerCurrent.y * 0.06

        cardBack.position.y = Math.sin(elapsed * 0.85) * 0.04
        scoreFace.position.y = cardBack.position.y
        cornerBadge.rotation.y = elapsed * 0.52
        badgeRing.rotation.z = elapsed * 0.7
        pendingOrb.rotation.x = elapsed * 0.6
        pendingOrb.rotation.y = elapsed * 0.85
        pendingOrb.position.y = 1.02 + Math.sin(elapsed * 1.2) * 0.08
        orbitOneGroup.rotation.z = orbitOneRotation.z + elapsed * 0.09
        orbitTwoGroup.rotation.z = orbitTwoRotation.z - elapsed * 0.075
        particles.rotation.y = elapsed * 0.028

        bars.forEach((bar, index) => {
          bar.position.y = barHeights[index] / 2 + 0.05 + Math.sin(elapsed * 1.25 + index) * 0.025
        })
        pulseNodes.forEach(({ mesh, phase, baseScale }) => {
          mesh.scale.setScalar(baseScale * (1 + Math.sin(elapsed * 1.6 + phase) * 0.15))
        })
        mintMaterial.emissiveIntensity = 0.72 + Math.sin(elapsed * 2.1) * 0.1
      }

      renderer.render(scene, camera)
    }

    const syncAnimation = () => {
      const shouldAnimate = !reduceMotion && isDocumentVisible && isInViewport
      renderer.setAnimationLoop(shouldAnimate ? renderFrame : null)
      if (!shouldAnimate) {
        resetPose()
        renderFrame()
      }
    }

    const handlePointerMove = (event: PointerEvent) => {
      if (reduceMotion || !isInViewport || mountRect.width === 0 || mountRect.height === 0) return
      const relativeX = (event.clientX - mountRect.left) / mountRect.width
      const relativeY = (event.clientY - mountRect.top) / mountRect.height
      pointerTarget.set((relativeX - 0.5) * 2, (relativeY - 0.5) * 2)
    }
    const handlePointerLeave = () => pointerTarget.set(0, 0)
    const handleViewportChange = () => {
      mountRect = mount.getBoundingClientRect()
    }
    const resizeScene = () => {
      mountRect = mount.getBoundingClientRect()
      const { width, height } = mountRect
      if (width === 0 || height === 0) return
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
      renderer.setSize(width, height, false)
      camera.aspect = width / height
      camera.updateProjectionMatrix()
      renderFrame()
    }
    const handleVisibilityChange = () => {
      isDocumentVisible = document.visibilityState !== 'hidden'
      syncAnimation()
    }
    const handleMotionPreference = (event: MediaQueryListEvent) => {
      reduceMotion = event.matches
      syncAnimation()
    }

    const resizeObserver = new ResizeObserver(resizeScene)
    resizeObserver.observe(mount)
    const intersectionObserver = new IntersectionObserver(
      ([entry]) => {
        isInViewport = entry.isIntersecting
        syncAnimation()
      },
      { threshold: 0.05 },
    )
    intersectionObserver.observe(mount)

    mount.addEventListener('pointermove', handlePointerMove, { passive: true })
    mount.addEventListener('pointerleave', handlePointerLeave)
    window.addEventListener('scroll', handleViewportChange, { passive: true })
    document.addEventListener('visibilitychange', handleVisibilityChange)
    motionPreference.addEventListener('change', handleMotionPreference)
    resizeScene()
    syncAnimation()

    return () => {
      renderer.setAnimationLoop(null)
      resizeObserver.disconnect()
      intersectionObserver.disconnect()
      mount.removeEventListener('pointermove', handlePointerMove)
      mount.removeEventListener('pointerleave', handlePointerLeave)
      window.removeEventListener('scroll', handleViewportChange)
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
      scoreTexture?.dispose()
      renderer.dispose()
      renderer.forceContextLoss()

      if (renderer.domElement.parentElement === mount) {
        mount.removeChild(renderer.domElement)
      }
      if (fallback) fallback.hidden = false
    }
  }, [averageScore, pendingCount])

  return (
    <div ref={mountRef} className="relative size-full touch-pan-y" aria-hidden="true">
      <div
        ref={fallbackRef}
        className="absolute top-1/2 left-1/2 w-[210px] -translate-x-1/2 -translate-y-1/2 rotate-[-3deg] rounded-2xl border border-[#9dc9de] bg-[linear-gradient(135deg,#0b315d,#14677b)] p-4 shadow-[0_18px_45px_rgba(20,83,112,0.2)] sm:w-[240px]"
      >
        <div className="h-1.5 w-14 rounded-full bg-[#55e0dc]" />
        <p className="mt-3 text-[9px] font-bold tracking-[0.12em] text-[#a8f5f1] uppercase">
          Academic score
        </p>
        <p className="mt-1 text-3xl font-black text-white">
          {averageScore === null ? '—' : averageScore.toFixed(1)}
          <span className="ml-1 text-xs font-semibold text-[#b8e3ec]">/10</span>
        </p>
        <div className="mt-3 flex items-center justify-between text-[9px] font-bold text-[#d6f7f5]">
          <span>LOCKED GRADES</span>
          <span>{pendingCount} PENDING</span>
        </div>
      </div>
    </div>
  )
}
